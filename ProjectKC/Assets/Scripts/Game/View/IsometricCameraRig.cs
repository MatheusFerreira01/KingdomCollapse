using UnityEngine;

namespace KingdomCollapse.Game
{
    /// <summary>
    /// Camera ortografica de angulo fixo (design D9). Nao roda nem orbita: o angulo
    /// unico elimina oclusao ambigua, clique ambiguo e enquadramento de UI, e permite
    /// que a arte futura seja desenhada para uma vista so.
    /// Ela apenas reenquadra quando o territorio cresce.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class IsometricCameraRig : MonoBehaviour
    {
        public static readonly Vector3 ViewAngle = new Vector3(35f, 45f, 0f);

        [SerializeField] private float _minSize = 4.5f;
        [SerializeField] private float _padding = 1.6f;
        [SerializeField] private float _followSpeed = 4f;

        private Camera _camera;
        private Vector3 _targetPivot;
        private float _targetSize;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _camera.orthographic = true;
            transform.rotation = Quaternion.Euler(ViewAngle);
            _targetSize = _minSize;
            _camera.orthographicSize = _minSize;
        }

        /// <summary>Enquadra os limites do reino, sem nunca apertar abaixo do minimo.</summary>
        public void Frame(Bounds bounds, bool instant = false)
        {
            _targetPivot = bounds.center;

            float extent = Mathf.Max(bounds.extents.x, bounds.extents.z);
            float needed = extent * _padding + 1f;
            _targetSize = Mathf.Max(_minSize, needed);

            if (instant)
            {
                ApplyImmediate();
            }
        }

        private void ApplyImmediate()
        {
            _camera.orthographicSize = _targetSize;
            transform.position = _targetPivot - transform.forward * 40f;
        }

        private void LateUpdate()
        {
            // Interpolacao curta: a camera acompanhar a expansao suaviza a leitura,
            // mas um salto instantaneo a cada compra faria o reino "pular".
            float t = 1f - Mathf.Exp(-_followSpeed * Time.deltaTime);
            Vector3 desired = _targetPivot - transform.forward * 40f;

            transform.position = Vector3.Lerp(transform.position, desired, t);
            _camera.orthographicSize = Mathf.Lerp(_camera.orthographicSize, _targetSize, t);
        }
    }
}
