using System;
using System.Collections.Generic;
using System.Linq;
using AnimatorAsCode.V1;
using JetBrains.Annotations;
using nadena.dev.ndmf;
using NdmfAsCode.V1.DBT;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace NdmfAsCode.V1
{
    public class AacPlugin<T> : Plugin<AacPlugin<T>> where T : MonoBehaviour
    {
        // Can be changed if necessary
        [PublicAPI] protected virtual string SystemName(Component script, BuildContext ctx) => GetType().Name;
        [PublicAPI] protected virtual Transform AnimatorRoot(Component script, BuildContext ctx) => ctx.AvatarRootTransform;
        [PublicAPI] protected virtual Transform DefaultValueRoot(Component script, BuildContext ctx) => ctx.AvatarRootTransform;
        [PublicAPI] protected virtual bool UseWriteDefaults(Component script, BuildContext ctx) => false;

        // This state is short-lived, it's really just sugar
        [PublicAPI] protected AacFlBase aac { get; private set; }
        [PublicAPI] protected T my { get; private set; }
        [PublicAPI] protected BuildContext context { get; private set; }

        public override string QualifiedName => $"dev.hai-vr.ndmf-processor::{GetType().FullName}";
        public override string DisplayName => $"NdmfAsCode for {GetType().Name}";

        protected virtual AacPluginOuput Execute()
        {
            return AacPluginOuput.Regular();
        }

        protected override void Configure()
        {
            if (GetType() == typeof(AacPlugin<>)) return;

            InPhase(BuildPhase.Generating)
                .BeforePlugin<>(NdmfAacDBTPlugin.Instance)
                .Run($"Run NdmfAsCode for {GetType().Name}", ctx =>
            {
                var results = new List<AacPluginOuput>();
                
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

                var state = ctx.GetState<InternalAacPluginState>();
                state.directBlendTreeMembers = results.SelectMany(output => output.members).ToArray();
            });
        }
    }

    internal class InternalAacPluginState
    {
        public AacPluginOuput.DirectBlendTreeMember[] directBlendTreeMembers;
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
            
            public string parameterOptional;
        }
    }
}