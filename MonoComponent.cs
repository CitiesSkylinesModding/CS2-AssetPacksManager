using UnityEngine;

namespace AssetPacksManager;

public class MonoComponent : MonoBehaviour
{
    void Awake()
    {
        DontDestroyOnLoad(transform.gameObject);
    }
}