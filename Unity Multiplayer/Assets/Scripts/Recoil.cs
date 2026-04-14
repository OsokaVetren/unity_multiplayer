using UnityEngine;

public class Recoil : MonoBehaviour
{
    private Vector3 currentRotation;
    private Vector3 targetRotation;

    // Эти значения будут обновляться при каждом выстреле из настроек оружия
    private float currentSnappiness;
    private float currentReturnSpeed;

    void Update()
    {
        // Используем параметры последнего использованного оружия для плавности
        targetRotation = Vector3.Lerp(targetRotation, Vector3.zero, currentReturnSpeed * Time.deltaTime);
        currentRotation = Vector3.Lerp(currentRotation, targetRotation, currentSnappiness * Time.deltaTime);
        
        transform.localRotation = Quaternion.Euler(currentRotation);
    }

    // ВАЖНО: Передаем сюда WeaponData
    public void FireRecoil(WeaponData data)
    {
        currentSnappiness = data.snappiness;
        currentReturnSpeed = data.returnSpeed;

        targetRotation += new Vector3(
            -data.recoilX, 
            Random.Range(-data.recoilY, data.recoilY), 
            Random.Range(-data.recoilZ, data.recoilZ)
        );
    }
}