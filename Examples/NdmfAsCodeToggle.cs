﻿#if UNITY_EDITOR
using ModularAvatarAsCode.V1;
using nadena.dev.ndmf;
using NdmfAsCode.V1.Example;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

[assembly: ExportsPlugin(typeof(AacToggleProcessor))]
namespace NdmfAsCode.V1.Example
{
    public class NdmfAsCodeToggle : MonoBehaviour
    {
        public string parameter;
        public GameObject[] objects;
        public Texture2D icon;
    }

    public class AacToggleProcessor : AacPlugin<NdmfAsCodeToggle>
    {
        protected override AacPluginOuput Execute()
        {
            var ctrl = aac.NewAnimatorController();
            var fx = ctrl.NewLayer();
            
            var off = fx.NewState("OFF").WithAnimation(aac.NewClip().Toggling(my.objects, false));
            var on = fx.NewState("ON").WithAnimation(aac.NewClip().Toggling(my.objects, true));

            var param = fx.BoolParameter(my.parameter);
            off.TransitionsTo(on).When(param.IsTrue());
            on.TransitionsTo(off).When(param.IsFalse());

            var maAc = MaAc.Create(my.gameObject);
            maAc.NewMergeAnimator(ctrl, VRCAvatarDescriptor.AnimLayerType.FX);
            maAc.NewParameter(param);
            maAc.EditMenuItemOnSelf().Toggle(param).Name(my.name).WithIcon(my.icon);

            return AacPluginOuput.Regular();
        }
    }
}
#endif