using UnityEngine;

namespace KingdomCollapse.Game
{
    /// <summary>
    /// Camera ortografica de angulo fixo (design D9). Nao roda nem orbita: o angulo
    /// unico elimina oclusao ambigua, clique ambiguo e enquadramento de UI, e permite
    /// que a arte futura seja desenhada para uma vista so.
    ///
    /// Ela reenquadra quando o territorio cresce, e enquadra na area LIVRE da tela:
    /// o HUD cobre as bordas, e centralizar o reino na tela inteira escondia metade
    /// dele atras dos paineis.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class IsometricCameraRig : MonoBehaviour
    {
        public static readonly Vector3 ViewAngle = new Vector3(35f, 45f, 0f);

        [SerializeField] private float _minSize = 2.5f;
        [SerializeField] private float _padding = 1.18f;
        [SerializeField] private float _followSpeed = 4f;

        private Camera _camera;
        private Vector3 _targetPivot;
        private float _targetSize;
        private Bounds _bounds = new Bounds(Vector3.zero, new Vector3(4f, 1f, 4f));

        // Fracao da tela coberta pelo HUD em cada borda.
        private float _insetLeft;
        private float _insetRight;
        private float _insetTop;
        private float _insetBottom;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _camera.orthographic = true;
            transform.rotation = Quaternion.Euler(ViewAngle);
            _targetSize = _minSize;
            _camera.orthographicSize = _minSize;
        }

        /// <summary>
        /// Informa quanto da tela o HUD cobre em cada borda, em fracao de 0 a 1.
        /// </summary>
        public void SetInsets(float left, float right, float top, float bottom, bool instant = false)
        {
            if (Mathf.Approximately(left, _insetLeft) && Mathf.Approximately(right, _insetRight) &&
                Mathf.Approximately(top, _insetTop) && Mathf.Approximately(bottom, _insetBottom))
            {
                return;
            }

            _insetLeft = Mathf.Clamp(left, 0f, 0.45f);
            _insetRight = Mathf.Clamp(right, 0f, 0.45f);
            _insetTop = Mathf.Clamp(top, 0f, 0.45f);
            _insetBottom = Mathf.Clamp(bottom, 0f, 0.45f);
            Frame(_bounds, instant);
        }

        /// <summary>Enquadra os limites do reino, sem nunca apertar abaixo do minimo.</summary>
        public void Frame(Bounds bounds, bool instant = false)
        {
            _bounds = bounds;

            // Tamanho do reino visto pela camera: projeta os oito cantos nos eixos
            // da tela em vez de supor que largura e profundidade pesam igual.
            Vector3 right = transform.right;
            Vector3 up = transform.up;
            float halfWidth = 0f;
            float halfHeight = 0f;

            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = new Vector3(
                    (i & 1) == 0 ? -bounds.extents.x : bounds.extents.x,
                    (i & 2) == 0 ? -bounds.extents.y : bounds.extents.y,
                    (i & 4) == 0 ? -bounds.extents.z : bounds.extents.z);
                halfWidth = Mathf.Max(halfWidth, Mathf.Abs(Vector3.Dot(corner, right)));
                halfHeight = Mathf.Max(halfHeight, Mathf.Abs(Vector3.Dot(corner, up)));
            }

            float freeWidth = Mathf.Max(0.2f, 1f - _insetLeft - _insetRight);
            float freeHeight = Mathf.Max(0.2f, 1f - _insetTop - _insetBottom);
            float aspect = _camera != null && _camera.pixelHeight > 0
                ? _camera.pixelWidth / (float)_camera.pixelHeight
                : 16f / 9f;

            float byHeight = halfHeight * _padding / freeHeight;
            float byWidth = halfWidth * _padding / (freeWidth * aspect);
            _targetSize = Mathf.Max(_minSize, Mathf.Max(byHeight, byWidth));

            // Desloca o centro para o meio da area livre.
            float shiftX = (_insetRight - _insetLeft) * _targetSize * aspect;
            float shiftY = (_insetTop - _insetBottom) * _targetSize;
            _targetPivot = bounds.center + right * shiftX + up * shiftY;

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
            float t = 1f - Mathf.Exp(-_followSpeed * Time.unscaledDeltaTime);
            Vector3 desired = _targetPivot - transform.forward * 40f;
            transform.position = Vector3.Lerp(transform.position, desired, t);
            _camera.orthographicSize = Mathf.Lerp(_camera.orthographicSize, _targetSize, t);
        }
    }
}
