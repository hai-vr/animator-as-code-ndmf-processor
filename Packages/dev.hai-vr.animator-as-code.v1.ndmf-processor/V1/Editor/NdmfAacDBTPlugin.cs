
using System;
using System.Collections.Generic;
using System.Linq;
using AnimatorAsCode.V1;
using nadena.dev.ndmf;
using NdmfAsCode.V1.DBT;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

[assembly: ExportsPlugin(typeof(NdmfAacDBTPlugin))]
namespace NdmfAsCode.V1.DBT
{
    public sealed class NdmfAacDBTPlugin : Plugin<NdmfAacDBTPlugin>
    {
        public override string QualifiedName => $"dev.hai-vr.ndmf-processor::dbt";
        public override string DisplayName => $"NdmfAsCode Create Shared Direct Blend Trees";

        protected override void Configure()
        {
            if (GetType() == typeof(AacPlugin<>)) return;
            
            InPhase(BuildPhase.Generating).Run($"NdmfAsCode Create Shared Direct Blend Trees", ctx =>
            {
                Debug.Log($"(self-log) Running aac direct blend tree builder ({GetType().FullName}");
                var aac = AacV1.Create(new AacConfiguration
                {
                    SystemName = "AacNdmfDBT",
                    AnimatorRoot = ctx.AvatarRootTransform,
                    DefaultValueRoot = ctx.AvatarRootTransform,
                    AssetKey = GUID.Generate().ToString(),
                    AssetContainer = ctx.AssetContainer,
                    DefaultsProvider = new AacDefaultsProvider(true)
                });

                var state = ctx.GetState<InternalAacPluginState>();
                if (state.directBlendTreeMembers == null) state.directBlendTreeMembers = Array.Empty<AacPluginOutput.DirectBlendTreeMember>();
                if (state.directBlendTreeOverrides == null) state.directBlendTreeOverrides = new Dictionary<string, float>();
                
                var playableLayerToController = new Dictionary<VRCAvatarDescriptor.AnimLayerType, AnimatorController>();
                foreach (var playableLayerToMember in state.directBlendTreeMembers.GroupBy(member => member.layerType))
                {
                    var playableLayer = playableLayerToMember.Key;

                    var ctrl = aac.NewAnimatorController();
                    var layer = ctrl.NewLayer();
                    var one = layer.FloatParameter("AacNdmf_One");
                    layer.OverrideValue(one, 1f);
                    
                    var dbt = aac.NewBlendTree()
                        .Direct();
                    
                    foreach (var member in playableLayerToMember)
                    {
                        dbt.WithAnimation(member.member, member.parameterOptional != null
                            ? layer.FloatParameter(member.parameterOptional)
                            : one);
                        var allBtParams = FindAllBTParams(member.member);
                        layer.FloatParameters(allBtParams);
                        foreach (var paramInBt in allBtParams)
                        {
                            if (state.directBlendTreeOverrides.TryGetValue(paramInBt, out var value))
                            {
                                layer.OverrideValue(layer.FloatParameter(paramInBt), value);
                            }
                        }
                    }
                
                    layer.NewState("DBT_AutoMerge")
                        // DBTs always use Write Defaults
                        .WithWriteDefaultsSetTo(true)
                        .WithAnimation(dbt); 
                    
                    playableLayerToController.Add(playableLayer, ctrl.AnimatorController);
                }
                
                if (playableLayerToController.Count > 0)
                {
                    var dbtHolder = new GameObject
                    {
                        name = "NdmfAacDBTs",
                        transform =
                        {
                            parent = ctx.AvatarRootTransform
                        }
                    };
                
                    // TODO: Make NDMF depend on Modular Avatar...???
                    // TODO: but what if the user uses VRCFury?
                    // var ma = MaAc.Create(dbtHolder);
                    // foreach (var dbt in playableLayerToController)
                    // {
                        // ma.NewMergeAnimator(dbt.Value, dbt.Key);
                    // }
                }
            });
        }

        private static string[] FindAllBTParams(Motion member)
        {
            return InternalFindAllBtParams(member)
                .Distinct()
                .Where(s => !string.IsNullOrEmpty(s))
                .ToArray();
        }

        private static IEnumerable<string> InternalFindAllBtParams(Motion member)
        {
            if (!(member is BlendTree blendTree)) return Array.Empty<string>();

            var parameters = ParametersOf(blendTree);

            return parameters.Concat(blendTree.children
                .Select(childMotion => childMotion.motion)
                .SelectMany(InternalFindAllBtParams));
        }

        private static IEnumerable<string> ParametersOf(BlendTree bt)
        {
            IEnumerable<string> parameters;
            switch (bt.blendType)
            {
                case BlendTreeType.Direct:
                    parameters = bt.children.Select(childMotion => childMotion.directBlendParameter);
                    break;
                case BlendTreeType.Simple1D:
                    parameters = new[] { bt.blendParameter };
                    break;
                case BlendTreeType.SimpleDirectional2D:
                case BlendTreeType.FreeformDirectional2D:
                case BlendTreeType.FreeformCartesian2D:
                default:
                    parameters = new[] { bt.blendParameter, bt.blendParameterY };
                    break;
            }

            return parameters;
        }
    }
}