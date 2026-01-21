using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PixmewStudios
{
    public interface HitableObjectsBase
    {
        internal void OnHit(Vector3 hitPosition);
    }
}
