using System.Collections.Generic;
using UnityEngine;

namespace DuckovProto.Runes.Data
{
    [CreateAssetMenu(menuName = "DuckovProto/Runes/Rune Library", fileName = "RuneLibrary")]
    public sealed class RuneLibrarySO : ScriptableObject
    {
        [SerializeField] private List<RuneDefinitionSO> runes = new List<RuneDefinitionSO>();

        private Dictionary<string, RuneDefinitionSO> byId;

        public IReadOnlyList<RuneDefinitionSO> Runes => runes;

        private void OnEnable()
        {
            RebuildCache();
        }

        private void OnValidate()
        {
            RebuildCache();
        }

        public bool TryGetById(string id, out RuneDefinitionSO rune)
        {
            rune = null;
            if (string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            if (byId == null)
            {
                RebuildCache();
            }

            return byId.TryGetValue(id, out rune);
        }

        public RuneDefinitionSO GetById(string id)
        {
            TryGetById(id, out RuneDefinitionSO rune);
            return rune;
        }

        private void RebuildCache()
        {
            if (byId == null)
            {
                byId = new Dictionary<string, RuneDefinitionSO>(64);
            }
            else
            {
                byId.Clear();
            }

            for (int i = 0; i < runes.Count; i++)
            {
                RuneDefinitionSO rune = runes[i];
                if (rune == null || string.IsNullOrWhiteSpace(rune.Id))
                {
                    continue;
                }

                byId[rune.Id] = rune;
            }
        }
    }
}
