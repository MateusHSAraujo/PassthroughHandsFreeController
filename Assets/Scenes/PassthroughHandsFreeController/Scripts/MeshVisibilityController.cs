using Meta.XR.MRUtilityKit;
using UnityEngine;

/// <summary>
/// Applies the MeshVisible setting to the MRUK EffectMesh.
/// Uses EffectMesh.HideMesh, which only toggles MeshRenderers: colliders stay active, so
/// CameraGazeCursor can still raycast against FLOOR_EffectMesh while the mesh is invisible.
/// The hideMesh flag is also honored by EffectMesh for surfaces spawned after this is applied.
/// </summary>
public class MeshVisibilityController : MonoBehaviour
{
    [SerializeField] private EffectMesh effectMesh;

    private void Awake()
    {
        if (effectMesh == null) effectMesh = FindAnyObjectByType<EffectMesh>();
        if (effectMesh == null) DebugLogger.LogError("MeshVisibilityController requires an EffectMesh reference.");
    }

    private void OnEnable()
    {
        SettingsStore.Changed += OnSettingsChanged;
        Apply(SettingsStore.Current.MeshVisible);
    }

    private void OnDisable()
    {
        SettingsStore.Changed -= OnSettingsChanged;
    }

    private void OnSettingsChanged(AppSettings settings)
    {
        Apply(settings.MeshVisible);
    }

    public void Apply(bool visible)
    {
        if (effectMesh == null) return;
        if (effectMesh.HideMesh == !visible) return;
        effectMesh.HideMesh = !visible;
        DebugLogger.Log($"Effect mesh visibility set to {visible}");
    }
}
