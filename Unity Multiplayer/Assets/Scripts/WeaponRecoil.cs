using UnityEngine;

public class WeaponRecoil : MonoBehaviour
{
    [Header("Kick settings")]
    public Vector3 positionKick = new Vector3(0f, 0f, -0.08f);
    public Vector3 rotationKick = new Vector3(-3f, 1.5f, 1.5f);

    [Header("Return settings")]
    public float returnSpeed = 10f;
    public float snappiness = 15f;

    private Vector3 targetPosition;
    private Vector3 currentPosition;

    private Vector3 targetRotation;
    private Vector3 currentRotation;

    private Vector3 initialLocalPosition;
    private Quaternion initialLocalRotation;

    private void Awake()
    {
        // сохраняем исходную позу оружия
        initialLocalPosition = transform.localPosition;
        initialLocalRotation = transform.localRotation;
    }

    public void Fire()
    {
        // отдача назад
        targetPosition += positionKick;

        // случайный разброс вращения
        targetRotation += new Vector3(
            rotationKick.x,
            Random.Range(-rotationKick.y, rotationKick.y),
            Random.Range(-rotationKick.z, rotationKick.z)
        );
    }

    private void Update()
    {
        // плавное возвращение к нулю
        targetPosition = Vector3.Lerp(targetPosition, Vector3.zero, returnSpeed * Time.deltaTime);
        targetRotation = Vector3.Lerp(targetRotation, Vector3.zero, returnSpeed * Time.deltaTime);

        currentPosition = Vector3.Lerp(currentPosition, targetPosition, snappiness * Time.deltaTime);
        currentRotation = Vector3.Lerp(currentRotation, targetRotation, snappiness * Time.deltaTime);

        // применяем ОТНОСИТЕЛЬНО исходной позы (ВАЖНО)
        transform.localPosition = initialLocalPosition + currentPosition;
        transform.localRotation = initialLocalRotation * Quaternion.Euler(currentRotation);
    }
}