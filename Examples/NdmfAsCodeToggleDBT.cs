#if UNITY_EDITOR
using ModularAvatarAsCode.V1;
using nadena.dev.ndmf;
using NdmfAsCode.V1.Example;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

[assembly: ExportsPlugin(typeof(AacToggleProcessor))]
namespace NdmfAsCode.V1.Example
{
    public class NdmfAsCodeToggleDBT : MonoBehaviour
    {
        public string parameter;
        public GameObject[] objects;
        public Texture2D icon;
    }

    public class AacToggleDBTProcessor : AacPlugin<NdmfAsCodeToggleDBT>
    {
        protected override AacPluginOuput Execute()
        {
            // Since this does not produce a layer but still needs Float parameters, use NoAnimator().
            // NDMF Processor will create the necessary parameters into the direct blend tree animator.
            var param = aac.NoAnimator().FloatParameter(my.parameter);
            
            var bt = aac.NewBlendTree()
                .Simple1D(param)
                .WithAnimation(aac.NewClip().Toggling(my.objects, false), 0)
                .WithAnimation(aac.NewClip().Toggling(my.objects, true), 1);

            var maAc = MaAc.Create(my.gameObject);
            // Blend Trees use a Float parameter, but the Expression Parameter can declare it as a bool.
            // Use the functions NewBoolToFloatParameter(...) and ToggleBoolToFloat(...) to reuse the parameter
            maAc.NewBoolToFloatParameter(param);
            maAc.EditMenuItemOnSelf().ToggleBoolToFloat(param).Name(my.name).WithIcon(my.icon);
            
            // TODO: We need a way to store override values! Such as One = 1, or Smoothing = 0.8.
            // This may need to be added in the output object
            return AacPluginOuput.DirectBlendTree(VRCAvatarDescriptor.AnimLayerType.FX, bt);
        }
    }
}
#endif