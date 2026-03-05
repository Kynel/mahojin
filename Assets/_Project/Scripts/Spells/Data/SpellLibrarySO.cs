using System.Collections.Generic;
using UnityEngine;

namespace DuckovProto.Spells.Data
{
    [CreateAssetMenu(menuName = "DuckovProto/Spells/Spell Library", fileName = "SpellLibrary")]
    public sealed class SpellLibrarySO : ScriptableObject
    {
        [SerializeField] private List<SpellDefinitionSO> spells = new List<SpellDefinitionSO>();

        private Dictionary<string, SpellDefinitionSO> byId;

        public IReadOnlyList<SpellDefinitionSO> Spells => spells;

        private void OnEnable()
        {
            RebuildCache();
        }

        private void OnValidate()
        {
            RebuildCache();
        }

        public bool TryGetById(string id, out SpellDefinitionSO spell)
        {
            spell = null;
            if (string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            if (byId == null)
            {
                RebuildCache();
            }

            return byId.TryGetValue(id, out spell);
        }

        public SpellDefinitionSO GetById(string id)
        {
            TryGetById(id, out SpellDefinitionSO spell);
            return spell;
        }

        private void RebuildCache()
        {
            if (byId == null)
            {
                byId = new Dictionary<string, SpellDefinitionSO>(64);
            }
            else
            {
                byId.Clear();
            }

            for (int i = 0; i < spells.Count; i++)
            {
                SpellDefinitionSO spell = spells[i];
                if (spell == null || string.IsNullOrWhiteSpace(spell.Id))
                {
                    continue;
                }

                byId[spell.Id] = spell;
            }
        }
    }
}
