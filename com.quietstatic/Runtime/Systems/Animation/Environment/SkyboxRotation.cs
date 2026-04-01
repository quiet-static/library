using UnityEngine;

public class SkyboxRotation : MonoBehaviour
{
    public float rotationSpeed = 1f;

    float currentRotation;

    void Update()
    {
        currentRotation += rotationSpeed * Time.deltaTime;
        RenderSettings.skybox.SetFloat("_Rotation", currentRotation);
    }
}