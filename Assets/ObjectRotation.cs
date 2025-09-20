using UnityEngine;

/// <summary>
/// カーソルキーで親オブジェクトを回転させ、離したらバネのように減衰しながら正面に戻す。
/// </summary>
public class ObjectRotation : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private CustomGridMesh customGridMesh;
    [SerializeField] private bool requireMeshCreated = true;

    [Header("Rotation")]
    [SerializeField, Range(0f, 90f)] private float maxAngle = 30f;
    [SerializeField] private float inputAcceleration = 120f; // [deg/s^2]
    [SerializeField] private bool invertHorizontal;
    [SerializeField] private bool invertVertical;

    [Header("Spring")]
    [SerializeField] private float returnFrequencyHz = 1.3f;  // Higher -> quicker return oscillation
    [SerializeField, Range(0f, 1f)] private float returnDampingRatio = 0.1f; // <1 keeps some bounce
    [SerializeField] private float driveDamping = 5f;         // [1/s] active while input is held down

    [Header("Integration")]
    [SerializeField] private float maxIntegrationStep = 1f / 90f; // [s] largest substep to integrate per loop
    [SerializeField] private float maxCatchUpTime = 0.5f;         // [s] cap on accumulated dt after stalls

    [Header("Pivot Offset")]
    [SerializeField] private bool syncPivotWithCamera = false;
    [SerializeField] private LookingGlassGoCamera cameraController;
    [SerializeField] private Transform meshRoot;

    private float pitch;          // rotation around X axis (degrees)
    private float yaw;            // rotation around Y axis (degrees)
    private float pitchVelocity;  // [deg/s]
    private float yawVelocity;    // [deg/s]
    private Quaternion lastAppliedRotation;
    private Vector3 parentInitialLocalPosition;
    private Vector3 meshInitialLocalPosition;
    private float cameraInitialZ;
    private bool pivotBaselineCaptured;

    private void OnValidate()
    {
        returnFrequencyHz = Mathf.Max(0.01f, returnFrequencyHz);
        maxIntegrationStep = Mathf.Max(1e-4f, maxIntegrationStep);
        maxCatchUpTime = Mathf.Max(maxIntegrationStep, maxCatchUpTime);
    }

    private void Awake()
    {
        SyncWithTransform();
    }

    private void Reset()
    {
        // Try to auto-fill when the script is first added in the editor.
        if (customGridMesh == null)
        {
            customGridMesh = GetComponentInChildren<CustomGridMesh>();
        }

        if (meshRoot == null && transform.childCount > 0)
        {
            meshRoot = transform.GetChild(0);
        }

        SyncWithTransform();
    }

    private void Start()
    {
        TryCapturePivotBaseline();
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f)
        {
            return;
        }

        // If some other script changed our rotation, adopt it as the new baseline.
        SyncWithTransformIfExternallyModified();

        if (syncPivotWithCamera)
        {
            TryCapturePivotBaseline();
            ApplyPivotOffset();
        }

        bool allowInput = true;
        if (requireMeshCreated)
        {
            if (customGridMesh == null)
            {
                allowInput = false;
            }
            else
            {
                allowInput = customGridMesh.IsMeshCreated;
            }
        }

        float pitchInput = allowInput ? GetAxis(KeyCode.UpArrow, KeyCode.DownArrow) : 0f;
        float yawInput = allowInput ? GetAxis(KeyCode.RightArrow, KeyCode.LeftArrow) : 0f;

        if (invertVertical)
        {
            pitchInput *= -1f;
        }
        if (invertHorizontal)
        {
            yawInput *= -1f;
        }

        UpdateAxis(ref pitch, ref pitchVelocity, pitchInput, dt);
        UpdateAxis(ref yaw, ref yawVelocity, yawInput, dt);

        lastAppliedRotation = Quaternion.Euler(pitch, yaw, 0f);
        transform.localRotation = lastAppliedRotation;
    }

    private static float GetAxis(KeyCode positiveKey, KeyCode negativeKey)
    {
        float value = 0f;
        if (Input.GetKey(positiveKey))
        {
            value += 1f;
        }
        if (Input.GetKey(negativeKey))
        {
            value -= 1f;
        }
        return value;
    }

    private void UpdateAxis(ref float angle, ref float velocity, float input, float dt)
    {
        float remaining = Mathf.Min(dt, maxCatchUpTime);
        float stepSize = maxIntegrationStep;
        int safety = 0;
        while (remaining > 0f && safety++ < 1024)
        {
            float step = Mathf.Min(remaining, stepSize);
            IntegrateAxisStep(ref angle, ref velocity, input, step);
            remaining -= step;
        }
    }

    private void IntegrateAxisStep(ref float angle, ref float velocity, float input, float dt)
    {
        bool hasInput = Mathf.Abs(input) > float.Epsilon;
        float acceleration = input * inputAcceleration;

        if (hasInput)
        {
            acceleration -= driveDamping * velocity;
        }
        else
        {
            float angularFrequency = Mathf.Max(0.01f, returnFrequencyHz) * (Mathf.PI * 2f);
            float springStrength = angularFrequency * angularFrequency;
            float damping = 2f * Mathf.Clamp01(returnDampingRatio) * angularFrequency;
            acceleration -= springStrength * angle + damping * velocity;
        }

        velocity += acceleration * dt;
        angle += velocity * dt;

        if (angle > maxAngle)
        {
            angle = maxAngle;
            if (velocity > 0f)
            {
                velocity = 0f;
            }
        }
        else if (angle < -maxAngle)
        {
            angle = -maxAngle;
            if (velocity < 0f)
            {
                velocity = 0f;
            }
        }

        if (!hasInput)
        {
            const float angleSnapThreshold = 0.05f;
            const float velocitySnapThreshold = 0.5f;
            if (Mathf.Abs(angle) <= angleSnapThreshold && Mathf.Abs(velocity) <= velocitySnapThreshold)
            {
                angle = 0f;
                velocity = 0f;
            }
        }
    }

    private void TryCapturePivotBaseline()
    {
        if (pivotBaselineCaptured || !syncPivotWithCamera)
        {
            return;
        }

        if (cameraController != null && !cameraController.IsInitialized)
        {
            return;
        }

        parentInitialLocalPosition = transform.localPosition;
        if (meshRoot != null)
        {
            meshInitialLocalPosition = meshRoot.localPosition;
        }

        cameraInitialZ = cameraController != null ? cameraController.CameraZ : transform.position.z;
        pivotBaselineCaptured = true;
    }

    private void ApplyPivotOffset()
    {
        if (!pivotBaselineCaptured)
        {
            return;
        }

        float currentCameraZ = GetCurrentCameraZ();
        float delta = currentCameraZ - cameraInitialZ;

        Vector3 parentPos = transform.localPosition;
        float targetParentZ = parentInitialLocalPosition.z + delta;
        if (!Mathf.Approximately(parentPos.z, targetParentZ))
        {
            parentPos.z = targetParentZ;
            transform.localPosition = parentPos;
        }

        if (meshRoot != null)
        {
            Vector3 meshPos = meshRoot.localPosition;
            float targetMeshZ = meshInitialLocalPosition.z - delta;
            if (!Mathf.Approximately(meshPos.z, targetMeshZ))
            {
                meshPos.z = targetMeshZ;
                meshRoot.localPosition = meshPos;
            }
        }
    }

    private float GetCurrentCameraZ()
    {
        if (cameraController != null && cameraController.IsInitialized)
        {
            return cameraController.CameraZ;
        }

        return cameraInitialZ;
    }

    private void SyncWithTransform()
    {
        lastAppliedRotation = transform.localRotation;
        Vector3 currentEuler = lastAppliedRotation.eulerAngles;
        pitch = NormalizeAngle(currentEuler.x);
        yaw = NormalizeAngle(currentEuler.y);
        pitchVelocity = 0f;
        yawVelocity = 0f;
    }

    private void SyncWithTransformIfExternallyModified()
    {
        if (Quaternion.Angle(transform.localRotation, lastAppliedRotation) > 0.05f)
        {
            SyncWithTransform();
        }
    }

    private static float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle > 180f)
        {
            angle -= 360f;
        }
        if (angle < -180f)
        {
            angle += 360f;
        }
        return angle;
    }
}
