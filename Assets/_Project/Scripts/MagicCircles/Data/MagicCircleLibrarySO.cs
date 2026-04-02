using System.Collections.Generic;
using UnityEngine;

namespace DuckovProto.MagicCircles.Data
{
    [CreateAssetMenu(menuName = "DuckovProto/Magic Circles/Magic Circle Library", fileName = "MagicCircleLibrary")]
    public sealed class MagicCircleLibrarySO : ScriptableObject
    {
        [SerializeField] private List<MagicCircleDefinitionSO> circles = new List<MagicCircleDefinitionSO>();

        private Dictionary<string, MagicCircleDefinitionSO> byId;

        public IReadOnlyList<MagicCircleDefinitionSO> Circles => circles;

        private void OnEnable()
        {
            RebuildCache();
        }

        private void OnValidate()
        {
            RebuildCache();
        }

        public bool TryGetById(string id, out MagicCircleDefinitionSO circle)
        {
            circle = null;
            if (string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            if (byId == null)
            {
                RebuildCache();
            }

            return byId.TryGetValue(id, out circle);
        }

        public MagicCircleDefinitionSO GetById(string id)
        {
            TryGetById(id, out MagicCircleDefinitionSO circle);
            return circle;
        }

        private void RebuildCache()
        {
            if (byId == null)
            {
                byId = new Dictionary<string, MagicCircleDefinitionSO>(32);
            }
            else
            {
                byId.Clear();
            }

            if (circles == null)
            {
                circles = new List<MagicCircleDefinitionSO>();
                return;
            }

            for (int i = 0; i < circles.Count; i++)
            {
                MagicCircleDefinitionSO circle = circles[i];
                if (circle == null || string.IsNullOrWhiteSpace(circle.Id))
                {
                    continue;
                }

                byId[circle.Id] = circle;
            }
        }
    }
}
