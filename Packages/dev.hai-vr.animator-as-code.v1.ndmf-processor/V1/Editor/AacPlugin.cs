using System;
using System.Collections.Generic;
using System.Linq;
using AnimatorAsCode.V1;
using JetBrains.Annotations;
using nadena.dev.ndmf;
using NdmfAsCode.V1.DBT;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace NdmfAsCode.V1
{
    public class AacPlugin<T> : Plugin<AacPlugin<T>> where T : MonoBehaviour
    {
        // Can be changed if necessary
        [PublicAPI] protected virtual string SystemName(T script, BuildContext ctx) => typeof(T).Name;
        [PublicAPI] protected virtual Transform AnimatorRoot(T script, BuildContext ctx) => ctx.AvatarRootTransform;
        [PublicAPI] protected virtual Transform DefaultValueRoot(T script, BuildContext ctx) => ctx.AvatarRootTransform;
        [PublicAPI] protected virtual bool UseWriteDefaults(T script, BuildContext ctx) => false;

        // This state is short-lived, it's really just sugar
        [PublicAPI] protected AacFlBase aac { get; private set; }
        [PublicAPI] protected T my { get; private set; }
        [PublicAPI] protected BuildContext context { get; private set; }

        public override string QualifiedName => $"dev.hai-vr.ndmf-processor::{GetType().FullName}";
        public override string DisplayName => $"NdmfAsCode for {GetType().Name}";

        protected virtual AacPluginOutput Execute()
        {
            return AacPluginOutput.Regular();
        }

        protected override void Configure()
        {
            if (GetType() == typeof(AacPlugin<>)) return;

            InPhase(BuildPhase.Generating)
                .Run($"Run NdmfAsCode for {GetType().Name}", ctx =>
                {
                    // Generating controllers requires that we have a persistent container
                    // so that Unity can create inner states, etc.
                    var persistentContainer = EnsureContainerCreated(ctx); 
                    
                    Debug.Log($"(self-log) Running aac plugin ({GetType().FullName}");
                    var results = new List<AacPluginOutput>();

                    var scripts = ctx.AvatarRootObject.GetComponentsInChildren(typeof(T), true);
                    foreach (var currentScript in scripts)
                    {
                        var script = (T)currentScript;
                        aac = AacV1.Create(new AacConfiguration
                        {
                            SystemName = SystemName(script, ctx),
                            AnimatorRoot = AnimatorRoot(script, ctx),
                            DefaultValueRoot = DefaultValueRoot(script, ctx),
                            AssetKey = GUID.Generate().ToString(),
                            AssetContainer = persistentContainer,
                            // GenericAssetContainer = ctx.AssetContainer,
                            DefaultsProvider = new AacDefaultsProvider(UseWriteDefaults(script, ctx))
                        });
                        my = script;
                        context = ctx;

                        Execute();
                    }

                    // Get rid of the short-lived sugar fields
                    aac = null;
                    my = null;
                    context = null;

                    var state = ctx.GetState<InternalAacPluginState>();
                    state.directBlendTreeMembers = results.SelectMany(output => output.members).ToArray();
                })
                .BeforePlugin((NdmfAacDBTPlugin)NdmfAacDBTPlugin.Instance);
        }

        private AnimatorController EnsureContainerCreated(BuildContext buildContext)
        {
            var state = buildContext.GetState<InternalAacPluginContainerState>();
            if (state.garbage != null) return state.garbage;

            var container = new AnimatorController();
            container.name =
                $"zAacGarbage-{DateTime.Now.ToString("yyyy'-'MM'-'dd'_'HHmmss")}-{GUID.Generate().ToString().Substring(0, 8)}";
            AssetDatabase.CreateAsset(container, $"Assets/{container.name}.asset");

            state.garbage = container;

            return container;
        }
    }

    internal class InternalAacPluginContainerState
    {
        public AnimatorController garbage;
    }

    internal class InternalAacPluginState
    {
        public AacPluginOutput.DirectBlendTreeMember[] directBlendTreeMembers;
    }

    public struct AacPluginOutput
    {
        public DirectBlendTreeMember[] members;

        public static AacPluginOutput Regular()
        {
            return new AacPluginOutput
            {
                members = Array.Empty<DirectBlendTreeMember>()
            };
        }

        public static AacPluginOutput DirectBlendTree(VRCAvatarDescriptor.AnimLayerType layerType, params AacFlBlendTree[] members)
        {
            return DirectBlendTree(layerType, members.Select(tree => (Motion)tree.BlendTree).ToArray());
        }

        public static AacPluginOutput DirectBlendTree(VRCAvatarDescriptor.AnimLayerType layerType, params Motion[] members)
        {
            return new AacPluginOutput
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