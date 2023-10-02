using System;
using System.Linq;
using AnimatorAsCode.V1;
using nadena.dev.ndmf;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace NdmfAsCode.V1
{
    // Can't be abstract
    // and also, can't be generic, it crashes
    public class AacPlugin<T> : Plugin<AacPlugin<T>> where T : MonoBehaviour
    // public class NdmfAsCodePlugin : Plugin<NdmfAsCodePlugin>
    {
        // Must be changed
        // protected virtual Type ScriptType => GetType().GetCustomAttribute<NdmfAsCode>(false).ScriptType;
        
        // Can be changed if necessary
        protected virtual string SystemName(Component script, BuildContext context) => GetType().Name;
        protected virtual Transform AnimatorRoot(Component script, BuildContext context) => context.AvatarRootTransform;
        protected virtual Transform DefaultValueRoot(Component script, BuildContext context) => context.AvatarRootTransform;
        protected virtual bool UseWriteDefaults(Component script, BuildContext context) => false;

        //
        
        // This state is short-lived, it's really just sugar
        protected AacFlBase aac { get; private set; }
        protected T my { get; private set; }
        protected BuildContext context { get; private set; }

        public override string QualifiedName => $"dev.hai-vr.ndmf-processor::{GetType().FullName}";
        public override string DisplayName => $"NdmfAsCode for {GetType().Name}";

        protected virtual AacPluginOuput Execute()
        {
            return AacPluginOuput.Regular();
        }

        protected override void Configure()
        {
            if (GetType() == typeof(AacPlugin<>)) return;
            
            InPhase(BuildPhase.Generating).Run($"Run NdmfAsCode for {GetType().Name}", ctx =>
            {
                var scripts = ctx.AvatarRootObject.GetComponentsInChildren(typeof(T), true);
                foreach (var currentScript in scripts)
                {
                    aac = AacV1.Create(new AacConfiguration
                    {
                        SystemName = SystemName(currentScript, ctx),
                        AnimatorRoot = AnimatorRoot(currentScript, ctx),
                        DefaultValueRoot = DefaultValueRoot(currentScript, ctx),
                        AssetKey = GUID.Generate().ToString(),
                        GenericAssetContainer = ctx.AssetContainer,
                        DefaultsProvider = new AacDefaultsProvider(UseWriteDefaults(currentScript, ctx))
                    });
                    my = (T)currentScript;
                    context = ctx;
                    Execute();
                }

                // Get rid of the short-lived sugar fields
                aac = null;
                my = null;
                context = null;
            });
        }
    }

    public struct AacPluginOuput
    {
        public DirectBlendTreeMember[] members;

        public static AacPluginOuput Regular()
        {
            return new AacPluginOuput
            {
                members = Array.Empty<DirectBlendTreeMember>()
            };
        }

        public static AacPluginOuput DirectBlendTree(VRCAvatarDescriptor.AnimLayerType layerType, params AacFlBlendTree[] members)
        {
            return DirectBlendTree(layerType, members.Select(tree => (Motion)tree.BlendTree).ToArray());
        }

        public static AacPluginOuput DirectBlendTree(VRCAvatarDescriptor.AnimLayerType layerType, params Motion[] members)
        {
            return new AacPluginOuput
            {
                members = members
                    .Select(motion => new DirectBlendTreeMember
                    {
                        layerType = layerType,
                        member = motion
                    })
                    .ToArray()
            };
        }
    
        public struct DirectBlendTreeMember
        {
            public VRCAvatarDescriptor.AnimLayerType layerType;
            public Motion member;
            
            public AacFlFloatParameter parameterOptional;
        }
    }
}