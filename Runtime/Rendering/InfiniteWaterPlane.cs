using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using WaterSystem.Settings;
using UnityEngine.Rendering.RenderGraphModule;

namespace WaterSystem.Rendering
{
    public class InfiniteWaterPlane : ScriptableRenderPass
    {
        private PassData _passData = new PassData();
        
        private class PassData
        {
            public Mesh InfiniteMesh;
            public Shader InfiniteShader;
            public Material InfiniteMaterial;
            public MaterialPropertyBlock MPB;
            public Matrix4x4 Matrix;
        }

        public Material Material;
        public float HeightOffset;
        private readonly MaterialPropertyBlock _mpb = new MaterialPropertyBlock();
        private readonly SphericalHarmonicsL2[] _ambientProbe = new SphericalHarmonicsL2[1];

        private const string PassName = nameof(InfiniteWaterPlane);

        public InfiniteWaterPlane()
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
            profilingSampler = new ProfilingSampler(PassName);

            Material = CoreUtils.CreateEngineMaterial(ProjectSettings.Instance.resources.InfiniteWaterShader);
        }
        
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            
            if (!ExecutionCheck(cameraData.camera)) return;
            
            using (var builder = renderGraph.AddRasterRenderPass<PassData>(PassName, out var passData, profilingSampler))
            {
                SetupPassData(ref passData, cameraData.worldSpaceCameraPos);
                
                var resourceData = frameData.Get<UniversalResourceData>();
                builder.SetRenderAttachment(resourceData.cameraColor, 0,AccessFlags.ReadWrite);
                // builder.SetRenderAttachment(resourceData.cameraDepth, 1,AccessFlags.ReadWrite);
                
                builder.AllowPassCulling(false);
                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    context.cmd.DrawMesh(data.InfiniteMesh, data.Matrix, data.InfiniteMaterial, 0, 0, data.MPB);
                });
            }
        }

        private void SetupPassData(ref PassData data, Vector3 cameraPositionWS)
        {
            data.InfiniteMesh = ProjectSettings.Instance.resources?.InfiniteWaterMesh;
            data.InfiniteMaterial = Material;
            data.Matrix = Matrix4x4.TRS(cameraPositionWS, Quaternion.identity, Vector3.one);
            data.MPB = _mpb;
            _ambientProbe[0] = RenderSettings.ambientProbe;
            data.MPB.CopySHCoefficientArraysFrom(_ambientProbe);
            data.MPB.SetFloat(Utilities.ShaderIDs.WaveHeight, HeightOffset);
            data.MPB.SetFloat(Utilities.ShaderIDs.MaxWaveHeight, 0); // todo add max wave height
        }

        private bool ExecutionCheck(Camera camera)
        {
            var cameraType = camera.cameraType;
            if (cameraType is not CameraType.Game or CameraType.SceneView && camera.name.Contains("Reflections"))
            {
                //Debug.Log($"Infinite water plane Skipping Camera {cameraData.camera.name}");
                return false;
            }
            
            if (!ProjectSettings.Instance.resources?.InfiniteWaterMesh)
            {
                Debug.LogError($"Infinite water place pass skipping due to missing Mesh or Material");
                return false;
            }

            return true;
        }

        public void Cleanup()
        {
            CoreUtils.Destroy(Material);
        }
    }
}