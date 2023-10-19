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
                            AssetContainer = ctx.AssetContainer,
                            ContainerMode = AacConfiguration.Container.OnlyWhenPersistenceRequired,
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

                    var overrides = new Dictionary<string, float>();
                    foreach (var result in results)
                    {
                        foreach (var over in result.overrides)
                        {
                            if (!overrides.ContainsKey(over.Key))
                            {
                                overrides.Add(over.Key, over.Value);
                            }
                        }
                    }
                    
                    var state = ctx.GetState<InternalAacPluginState>();
                    state.directBlendTreeMembers = results.SelectMany(output => output.members).ToArray();
                    state.directBlendTreeOverrides = overrides;
                })
                .BeforePlugin((NdmfAacDBTPlugin)NdmfAacDBTPlugin.Instance);
        }
    }

    internal class InternalAacPluginState
    {
        public AacPluginOutput.DirectBlendTreeMember[] directBlendTreeMembers;
        public Dictionary<string, float> directBlendTreeOverrides;
    }

    public struct AacPluginOutput
    {
        public DirectBlendTreeMember[] members;
        public Dictionary<string, float> overrides;

        public AacPluginOutput OverrideValue(AacFlParameter parameter, float value)
        {
            overrides.Add(parameter.Name, value);
            return this;
        }

        public AacPluginOutput OverrideValue(string parameter, float value)
        {
            overrides.Add(parameter, value);
            return this;
        }

        public static AacPluginOutput Regular()
        {
            return new AacPluginOutput
            {
                members = Array.Empty<DirectBlendTreeMember>(),
                overrides = new Dictionary<string, float>()
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
                    .ToArray(),
                overrides = new Dictionary<string, float>()
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