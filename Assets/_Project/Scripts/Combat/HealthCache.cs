using System.Collections.Generic;
using UnityEngine;

namespace DuckovProto.Combat
{
    public static class HealthCache
    {
        public static Health GetHealth(Collider collider)
        {
            if (collider == null)
            {
                return null;
            }

            if (collider.TryGetComponent(out HealthLink link))
            {
                return link.HealthComponent;
            }

            Health health = collider.GetComponentInParent<Health>();

            link = collider.gameObject.AddComponent<HealthLink>();
            link.HealthComponent = health;

            return health;
        }
    }
}
