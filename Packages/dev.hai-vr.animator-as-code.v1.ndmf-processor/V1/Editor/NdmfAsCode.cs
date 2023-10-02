using System;
using AnimatorAsCode.V1;
using nadena.dev.ndmf;
using UnityEditor;
using UnityEngine;

namespace NdmfAsCode.V1
{
    // Can't be abstract
    // and also, can't be generic, it crashes
    public class NdmfAsCodePlugin<T> : Plugin<NdmfAsCodePlugin<T>> where T : MonoBehaviour
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

        protected virtual NdmfAsCodeOutput Execute()
        {
            return NdmfAsCodeOutput.Regular();
        }

        protected override void Configure()
        {
            if (GetType() == typeof(NdmfAsCodePlugin<>)) return;
            
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

    // public interface INdmfAsCodeProcessor<T> where T : MonoBehaviour
    // {
        // void Execute(T current, BuildContext x);
    // }
    
    // [System.AttributeUsage(System.AttributeTargets.Class)]
    // public class NdmfAsCode : System.Attribute
    // {
    //     public Type ScriptType { get; }
    //
    //     public NdmfAsCode(Type ScriptType)
    //     {
    //         this.ScriptType = ScriptType;
    //     }
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

        public static NdmfAsCodeOutput MergeIntoDirectBlendTree(params Motion[] motionsToAddInSharedDirectBlendTree)
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