using UnityEngine;

/// <summary>
/// Простая метка точки спавна. Размещайте пустые GameObject'ы с этим компонентом на сцене.
/// GameManager автоматически найдёт их и будет использовать для респавна.
/// </summary>
public class NetworkSpawnPoint : MonoBehaviour
{
    [Tooltip("Отображать гизмо в редакторе?")]
    public bool drawGizmo = true;

    [Tooltip("Цвет гизмо")]
    public Color gizmoColor = new Color(0f, 1f, 0.4f, 0.85f);

    [Tooltip("Размер гизмо (метры)")]
    public float gizmoRadius = 0.5f;

    private void OnDrawGizmos()
    {
        if (!drawGizmo) return;
        Gizmos.color = gizmoColor;
        Gizmos.DrawSphere(transform.position, gizmoRadius);
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 1.5f);

        // Показать высоту "капсулы" игрока
        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.3f);
        Gizmos.DrawWireCube(transform.position + Vector3.up * 1f, new Vector3(0.6f, 2f, 0.6f));
    }
}
