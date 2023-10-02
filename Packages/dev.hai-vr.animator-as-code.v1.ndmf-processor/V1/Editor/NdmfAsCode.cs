using System;
using AnimatorAsCode.V1;
using nadena.dev.ndmf;
using UnityEditor;
using UnityEngine;

namespace NdmfAsCode.V1
{
    // Can't be abstract
    // public class AbstractNdmfAsCodePlugin<T> : Plugin<AbstractNdmfAsCodePlugin<T>> where T : MonoBehaviour
    public class AbstractNdmfAsCodePlugin : Plugin<AbstractNdmfAsCodePlugin>
    {
        // Must be changed
        protected virtual Type ScriptType => typeof(AbstractNdmfAsCodePlugin);
        
        // Can be changed if necessary
        protected virtual string SystemName(Component script, BuildContext context) => GetType().Name;
        protected virtual Transform AnimatorRoot(Component script, BuildContext context) => context.AvatarRootTransform;
        protected virtual Transform DefaultValueRoot(Component script, BuildContext context) => context.AvatarRootTransform;
        protected virtual bool UseWriteDefaults(Component script, BuildContext context) => false;

        //
        
        protected AacFlBase aac;
        protected Component script;
        protected BuildContext buildContext;
        
        public override string QualifiedName => $"dev.hai-vr.ndmf-processor::{GetType().FullName}";
        public override string DisplayName => $"NdmfAsCode for {GetType().Name}";

        protected virtual NdmfAsCodeOutput Execute()
        {
            return NdmfAsCodeOutput.Regular();
        }

        protected override void Configure()
        {
            if (GetType() == typeof(AbstractNdmfAsCodePlugin)) return;
            
            InPhase(BuildPhase.Generating).Run($"Run NdmfAsCode for {GetType().Name}", ctx =>
            {
                var scripts = ctx.AvatarRootObject.GetComponentsInChildren(ScriptType, true);
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
                    script = currentScript;
                    buildContext = ctx;
                    Execute();
                }
            });
        }
    }

    // public interface INdmfAsCodeProcessor<T> where T : MonoBehaviour
    // {
        // void Execute(T current, BuildContext x);
    // }

    public struct NdmfAsCodeOutput
    {
        public Motion[] directBlendTreeMembers;
        public NdmfAsCodeWeightedDirectBlendTreeMember[] weightedMembers;

        public static NdmfAsCodeOutput Regular()
        {
            return new NdmfAsCodeOutput
            {
                directBlendTreeMembers = Array.Empty<Motion>(),
                weightedMembers = Array.Empty<NdmfAsCodeWeightedDirectBlendTreeMember>()
            };
        }

        public static NdmfAsCodeOutput MergeIntoDirectBlendTree(Motion[] motionsToAddInSharedDirectBlendTree)
        {
            return new NdmfAsCodeOutput
            {
                directBlendTreeMembers = motionsToAddInSharedDirectBlendTree,
                weightedMembers = Array.Empty<NdmfAsCodeWeightedDirectBlendTreeMember>()
            };
        }
    }
    
    public struct NdmfAsCodeWeightedDirectBlendTreeMember
    {
        public AacFlFloatParameter parameter;
        public Motion member;
    }
}