using DuckovProto.MagicCircles.Data;
using UnityEngine;

namespace DuckovProto.MagicCircles.Matching
{
    public sealed class MagicCircleMatchResult
    {
        public MagicCircleDefinitionSO Circle;
        public float Score;
        public bool Passed;
        public string Reason;
        public Vector2[] AlignedReference;
        public Vector2[] AlignedUser;
        public string MatchedId;
        public string MatchedDisplayName;
        public float Threshold;
        public float Rmse;
        public bool UsedReverse;
    }
}
