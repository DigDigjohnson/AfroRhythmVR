
using UnityEngine;
using UnityEngine.XR;

public class VRDrumHand : MonoBehaviour
{
    [Header("Configuracao")]
    [SerializeField] private bool isLeftHand;
    [SerializeField] private AtabaqueSurface drum;

    [Header("Deteccao")]
    [SerializeField] private float minimumSpeed = 0.3f;
    [SerializeField] private float cooldown = 0.12f;
    [SerializeField] private float resetHeight = 0.08f;

    [Header("Vibracao")]
    [Range(0f, 1f)]
    [SerializeField] private float hapticStrength = 0.45f;
    [SerializeField] private float hapticDuration = 0.06f;

    private Vector3 lastPosition;
    private bool initialized;
    private bool readyToHit = true;
    private float lastHitTime = -100f;

    private void OnEnable()
    {
        initialized = false;
        readyToHit = true;
    }

    private void Update()
    {
        if (drum == null)
            return;

        Vector3 position = transform.position;

        if (!initialized)
        {
            lastPosition = position;
            initialized = true;
            return;
        }

        Vector3 surface = drum.SurfaceCenter;

        // Permite uma nova batida quando a mao
        // voltar acima da superficie.
        if (position.y > surface.y + resetHeight)
            readyToHit = true;

        float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
        Vector3 velocity = (position - lastPosition) / deltaTime;

        bool crossedSurface =
            lastPosition.y >= surface.y &&
            position.y < surface.y;

        bool descending = velocity.y < -minimumSpeed;

        bool cooldownFinished =
            Time.time - lastHitTime >= cooldown;

        if (readyToHit &&
            crossedSurface &&
            descending &&
            cooldownFinished)
        {
            // Calcula onde a mao atravessou o plano.
            float fraction =
                (surface.y - lastPosition.y) /
                (position.y - lastPosition.y);

            Vector3 contact = Vector3.Lerp(
                lastPosition,
                position,
                Mathf.Clamp01(fraction)
            );

            Vector2 offset = new Vector2(
                contact.x - surface.x,
                contact.z - surface.z
            );

            if (offset.magnitude <= drum.Radius)
            {
                drum.Hit(isLeftHand);

                lastHitTime = Time.time;
                readyToHit = false;

                Vibrate();
            }
        }

        lastPosition = position;
    }

    private void Vibrate()
    {
        XRNode node = isLeftHand
            ? XRNode.LeftHand
            : XRNode.RightHand;

        InputDevice device =
            InputDevices.GetDeviceAtXRNode(node);

        if (!device.isValid)
            return;

        if (device.TryGetHapticCapabilities(
            out HapticCapabilities capabilities))
        {
            if (capabilities.supportsImpulse)
            {
                device.SendHapticImpulse(
                    0u,
                    hapticStrength,
                    hapticDuration
                );
            }
        }
    }
}
