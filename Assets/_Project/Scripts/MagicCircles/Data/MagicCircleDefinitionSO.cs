using UnityEngine;

namespace DuckovProto.MagicCircles.Data
{
    [CreateAssetMenu(menuName = "DuckovProto/Magic Circles/Magic Circle Definition", fileName = "MagicCircleDefinition_")]
    public sealed class MagicCircleDefinitionSO : ScriptableObject
    {
        [SerializeField] private string id = "magic_circle_id";
        [SerializeField] private string displayName = "Magic Circle";
        [SerializeField] private string description = "One-stroke magic circle.";
        [SerializeField] private string linkedSpellId = "spell_id";
        [SerializeField] private bool enabled = true;
        [SerializeField] private Color themeColor = new Color(0.85f, 0.92f, 1f, 1f);
        [SerializeField] private Vector2[] referenceStrokeNorm = new Vector2[0];
        [SerializeField] private float passThreshold = 0.7f;
        [SerializeField] private int resampleCount = 128;
        [SerializeField] private bool allowReverseStroke = true;
        [SerializeField] private bool autoNormalizeScale = true;
        [SerializeField] private bool autoNormalizeRotation = true;
        [SerializeField] private bool autoNormalizeTranslation = true;
        [SerializeField] private Sprite optionalPreviewSprite;
        [SerializeField] private string howToDrawHint = "Trace the sample line in one stroke.";

        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public string LinkedSpellId => linkedSpellId;
        public bool Enabled => enabled;
        public Color ThemeColor => themeColor;
        public Vector2[] ReferenceStrokeNorm => referenceStrokeNorm;
        public float PassThreshold => Mathf.Clamp01(passThreshold);
        public int ResampleCount => Mathf.Clamp(resampleCount, 16, 512);
        public bool AllowReverseStroke => allowReverseStroke;
        public bool AutoNormalizeScale => autoNormalizeScale;
        public bool AutoNormalizeRotation => autoNormalizeRotation;
        public bool AutoNormalizeTranslation => autoNormalizeTranslation;
        public Sprite OptionalPreviewSprite => optionalPreviewSprite;
        public string HowToDrawHint => howToDrawHint;
        public bool HasReferenceStroke => referenceStrokeNorm != null && referenceStrokeNorm.Length >= 2;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (referenceStrokeNorm == null)
            {
                referenceStrokeNorm = new Vector2[0];
            }

            passThreshold = Mathf.Clamp01(passThreshold);
            resampleCount = Mathf.Clamp(resampleCount, 16, 512);
        }
#endif
    }
}
