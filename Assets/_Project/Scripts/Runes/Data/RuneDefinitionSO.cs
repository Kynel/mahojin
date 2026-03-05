using System;
using System.Collections.Generic;
using UnityEngine;

namespace DuckovProto.Runes.Data
{
    [Flags]
    public enum ExtraRuleFlags
    {
        None = 0,
        StrictHorizontal = 1 << 0,
        FocusRightSide = 1 << 1,
        FocusIntersections = 1 << 2,
    }

    [Serializable]
    public struct StrictSegment
    {
        [Min(0)] public int startIdx;
        [Min(0)] public int endIdx;
        [Min(0f)] public float weight;
        [Min(0.01f)] public float tolMultiplier;
        public ExtraRuleFlags extraRule;
    }

    [Serializable]
    public struct Gate
    {
        public Vector2 pos;
        [Min(0.01f)] public float tol;
        [Min(0f)] public float weight;
    }

    [CreateAssetMenu(menuName = "DuckovProto/Runes/Rune Definition", fileName = "RuneDefinition_")]
    public sealed class RuneDefinitionSO : ScriptableObject
    {
        [SerializeField] private string id = "rune_id";
        [SerializeField] private string displayName = "Rune";
        [SerializeField] private Vector2[] guidePolylineNorm = Array.Empty<Vector2>();
        [SerializeField] private List<StrictSegment> strictSegments = new List<StrictSegment>();
        [SerializeField] private List<Gate> gates = new List<Gate>();
        [SerializeField] private float baseTol = 0.2f;
        [SerializeField] private float passScore = 0.6f;
        [SerializeField] private string linkedSpellId = "spell_id";
        [SerializeField] private ExtraRuleFlags extraRuleFlags = ExtraRuleFlags.None;
        [Header("Reference Stroke Comparison")]
        [SerializeField] private bool useReferenceStrokeComparison;
        [SerializeField] private Vector2[] referenceStrokeNorm = Array.Empty<Vector2>();
        [SerializeField] private float referenceRmseTol = 0.11f;
        [SerializeField] private float referenceChamferTol = 0.08f;
        [SerializeField] private bool enforceStrictHorizontalSegment = true;
        [SerializeField] private float strictAngleDeg = 10f;
        [SerializeField] private float strictYStd = 0.05f;
        [SerializeField] private int strictSegmentStartIdx = 44;
        [SerializeField] private int strictSegmentEndIdx = 84;

        public string Id => id;
        public string DisplayName => displayName;
        public IReadOnlyList<Vector2> GuidePolylineNorm => guidePolylineNorm;
        public IReadOnlyList<StrictSegment> StrictSegments => strictSegments;
        public IReadOnlyList<Gate> Gates => gates;
        public float BaseTol => Mathf.Max(0.01f, baseTol);
        public float PassScore => Mathf.Clamp01(passScore);
        public string LinkedSpellId => linkedSpellId;
        public ExtraRuleFlags ExtraRuleFlagMask => extraRuleFlags;
        public bool UseReferenceStrokeComparison => useReferenceStrokeComparison;
        public IReadOnlyList<Vector2> ReferenceStrokeNorm => referenceStrokeNorm;
        public float ReferenceRmseTol => Mathf.Max(0.0001f, referenceRmseTol);
        public float ReferenceChamferTol => Mathf.Max(0.0001f, referenceChamferTol);
        public bool EnforceStrictHorizontalSegment => enforceStrictHorizontalSegment;
        public float StrictAngleDeg => Mathf.Clamp(strictAngleDeg, 0f, 90f);
        public float StrictYStd => Mathf.Max(0f, strictYStd);
        public int StrictSegmentStartIdx => Mathf.Max(0, strictSegmentStartIdx);
        public int StrictSegmentEndIdx => Mathf.Max(0, strictSegmentEndIdx);
    }
}
