using System;
using System.Threading.Tasks;
using EFT.UI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ImprovedCustomizationUI.Customization.Preview
{
    public class PreviewCamera : IDisposable
    {
        public const float LEGS_DROP = 0.5f;

        public static readonly Vector3 WHOLE_BODY_POSITION = new Vector3(0f, 0.92f, -6.3f);

        private const float ZOOM_STEP = 0.2f;
        private const float MIN_DISTANCE = 0.5f;
        private const float MAX_ZOOM_OUT = 1f;
        private const float MAX_FREE_LOOK_DISTANCE = 4f;

        private const float ORBIT_SPEED = 0.3f;
        private const float MIN_PITCH = -45f;
        private const float MAX_PITCH = 80f;

        private const float PAN_SPEED = 0.0015f;
        private const float PAN_LIMIT = 0.8f;
        private const float PIVOT_LOWEST = 0.1f;
        private const float PIVOT_HIGHEST = 2f;

        private const float FOLLOW_SPEED = 14f;

        private readonly HeadSelectionState _head;
        private readonly PlayerProfilePreview _preview;
        private PreviewScrollCatcher _scrollCatcher;
        private bool _disposed;

        private PlayerProfilePreview.ECameraViewType _view = PlayerProfilePreview.ECameraViewType.Head;
        private float _drop;

        private int _cameraMove;
        private bool _moving;

        private float _zoomWanted;
        private float _zoomDone;

        private bool _freeLook;
        private Vector3 _pivot;
        private float _yaw;
        private float _pitch;
        private float _distance;
        private Vector3 _shownPivot;
        private float _shownYaw;
        private float _shownPitch;
        private float _shownDistance;

        private bool _loggedSpots;

        public bool FreeLook
        {
            get { return _freeLook; }
        }

        public bool MouseOverModel
        {
            get { return _scrollCatcher != null && _scrollCatcher.Hovered; }
        }

        public PreviewCamera(HeadSelectionState head)
        {
            _head = head;
            _preview = head._preview;
        }

        public void Attach()
        {
            GameObject dragArea = _preview.DragTrigger.gameObject;
            _scrollCatcher = dragArea.GetComponent<PreviewScrollCatcher>();
            if (_scrollCatcher == null)
            {
                _scrollCatcher = dragArea.AddComponent<PreviewScrollCatcher>();
            }
            _scrollCatcher.Scrolled = OnScroll;
        }

        public void MoveTo(PlayerProfilePreview.ECameraViewType view, float drop)
        {
            _view = view;
            _drop = drop;
            if (!_freeLook)
            {
                Move();
            }
        }

        public bool WholeBody { get; private set; }

        public void SetWholeBody(bool on)
        {
            WholeBody = on;
            if (_freeLook)
            {
                EndFreeLook();
            }
            else
            {
                Move();
            }
        }

        public void GoTo(PlayerProfilePreview.ECameraViewType view, float drop)
        {
            _view = view;
            _drop = drop;
            if (_freeLook)
            {
                EndFreeLook();
            }
            else
            {
                Move();
            }
        }

        public void SetFreeLook(bool on)
        {
            if (_disposed || on == _freeLook)
            {
                return;
            }

            if (on)
            {
                StartFreeLook();
            }
            else
            {
                EndFreeLook();
            }
        }

        public void Tick()
        {
            if (_disposed)
            {
                return;
            }

            float follow = 1f - Mathf.Exp(-FOLLOW_SPEED * Time.unscaledDeltaTime);

            if (_freeLook)
            {
                TickPan();
                UpdateFreeLook(follow);
                return;
            }

            if (_moving)
            {
                return;
            }

            float step = (_zoomWanted - _zoomDone) * follow;
            if (Mathf.Abs(step) < 0.0001f)
            {
                return;
            }

            Transform container = _preview._cameraContainer;
            Transform camera = _preview._camera.transform;
            Vector3 cameraLocal = container.InverseTransformPoint(camera.position);
            Vector3 middle = new Vector3(0f, cameraLocal.y, 0f);
            Vector3 towardsModel = (middle - cameraLocal).normalized;
            camera.position = container.TransformPoint(cameraLocal + towardsModel * step);
            _zoomDone += step;
        }

        private void OnScroll(PointerEventData eventData)
        {
            if (_disposed || eventData.scrollDelta.y == 0f)
            {
                return;
            }

            float notches = Mathf.Sign(eventData.scrollDelta.y);

            if (_freeLook)
            {
                _distance = Mathf.Clamp(_distance - notches * ZOOM_STEP, MIN_DISTANCE, MAX_FREE_LOOK_DISTANCE);
                return;
            }

            if (_moving)
            {
                return;
            }

            Transform container = _preview._cameraContainer;
            Vector3 cameraLocal = container.InverseTransformPoint(_preview._camera.transform.position);
            float distance = new Vector2(cameraLocal.x, cameraLocal.z).magnitude;
            float room = distance - MIN_DISTANCE;

            _zoomWanted = Mathf.Clamp(_zoomWanted + notches * ZOOM_STEP, -MAX_ZOOM_OUT, _zoomDone + room);
        }

        private void StartFreeLook()
        {
            Transform container = _preview._cameraContainer;
            Transform camera = _preview._camera.transform;

            if (_moving)
            {
                PlayerProfilePreview.ECameraViewType spot;
                if (FindSpot(_view, out spot))
                {
                    _preview.ChangeCameraPosition(spot);
                    camera.localPosition += new Vector3(0f, -_drop, 0f);
                }
            }
            _cameraMove++;
            _moving = false;

            _preview.DragTrigger.onDrag -= _head.DragHandler;
            _preview.DragTrigger.onDrag -= OnFreeLookDrag;
            _preview.DragTrigger.onDrag += OnFreeLookDrag;

            Vector3 cameraLocal = container.InverseTransformPoint(camera.position);
            Quaternion look = Quaternion.Inverse(container.rotation) * camera.rotation;
            Vector3 forward = look * Vector3.forward;

            float across = new Vector2(cameraLocal.x, cameraLocal.z).magnitude;
            float flatForward = new Vector2(forward.x, forward.z).magnitude;
            float pivotHeight = cameraLocal.y;
            if (flatForward > 0.1f)
            {
                pivotHeight += forward.y * (across / flatForward);
            }
            pivotHeight = Mathf.Clamp(pivotHeight, PIVOT_LOWEST, PIVOT_HIGHEST);
            Vector3 middle = new Vector3(0f, pivotHeight, 0f);

            float distance = Vector3.Distance(cameraLocal, middle);
            if (distance < MIN_DISTANCE)
            {
                distance = MIN_DISTANCE;
            }

            _shownYaw = look.eulerAngles.y;
            _shownPitch = SignedAngle(look.eulerAngles.x);
            _shownDistance = distance;
            _shownPivot = cameraLocal + forward * distance;

            _pivot = middle;
            _distance = distance;
            _yaw = _shownYaw;
            _pitch = _shownPitch;
            if (Vector3.Distance(cameraLocal, middle) > 0.01f)
            {
                Quaternion towardsModel = Quaternion.LookRotation(middle - cameraLocal);
                _yaw = towardsModel.eulerAngles.y;
                _pitch = SignedAngle(towardsModel.eulerAngles.x);
            }
            _pitch = Mathf.Clamp(_pitch, MIN_PITCH, MAX_PITCH);

            _freeLook = true;
        }

        private void EndFreeLook()
        {
            _freeLook = false;
            _panning = false;
            _preview.DragTrigger.onDrag -= OnFreeLookDrag;
            _preview.DragTrigger.onDrag -= _head.DragHandler;
            _preview.DragTrigger.onDrag += _head.DragHandler;
            Move();
        }

        private void OnFreeLookDrag(PointerEventData eventData)
        {
            if (!_freeLook || _disposed)
            {
                return;
            }

            if (eventData.button == PointerEventData.InputButton.Left)
            {
                Vector2 delta = eventData.delta;
                _yaw += delta.x * ORBIT_SPEED;
                _pitch = Mathf.Clamp(_pitch - delta.y * ORBIT_SPEED, MIN_PITCH, MAX_PITCH);
            }
        }

        private bool _panning;
        private Vector3 _lastMouse;

        private void TickPan()
        {
            bool held = Input.GetMouseButton(1) || Input.GetMouseButton(2);
            if (!_panning)
            {
                if ((Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2)) && MouseOverModel)
                {
                    _panning = true;
                    _lastMouse = Input.mousePosition;
                }
                return;
            }

            if (!held)
            {
                _panning = false;
                return;
            }

            Vector3 now = Input.mousePosition;
            Vector2 delta = new Vector2(now.x - _lastMouse.x, now.y - _lastMouse.y);
            _lastMouse = now;
            if (delta.sqrMagnitude > 0f)
            {
                Pan(delta);
            }
        }

        private void Pan(Vector2 delta)
        {
            Quaternion look = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 right = look * Vector3.right;
            Vector3 up = look * Vector3.up;
            float scale = _distance * PAN_SPEED;
            Vector3 pivot = _pivot - right * (delta.x * scale) - up * (delta.y * scale);

            pivot.x = Mathf.Clamp(pivot.x, -PAN_LIMIT, PAN_LIMIT);
            pivot.z = Mathf.Clamp(pivot.z, -PAN_LIMIT, PAN_LIMIT);
            pivot.y = Mathf.Clamp(pivot.y, PIVOT_LOWEST, PIVOT_HIGHEST);
            _pivot = pivot;
        }

        private void UpdateFreeLook(float follow)
        {
            _shownYaw = Mathf.LerpAngle(_shownYaw, _yaw, follow);
            _shownPitch = Mathf.Lerp(_shownPitch, _pitch, follow);
            _shownDistance = Mathf.Lerp(_shownDistance, _distance, follow);
            _shownPivot = Vector3.Lerp(_shownPivot, _pivot, follow);

            Quaternion look = Quaternion.Euler(_shownPitch, _shownYaw, 0f);
            Vector3 cameraLocal = _shownPivot + look * new Vector3(0f, 0f, -_shownDistance);

            Transform container = _preview._cameraContainer;
            Transform camera = _preview._camera.transform;
            camera.position = container.TransformPoint(cameraLocal);
            camera.rotation = container.rotation * look;
        }

        private static float SignedAngle(float angle)
        {
            return angle > 180f ? angle - 360f : angle;
        }

        private async void Move()
        {
            _cameraMove++;
            int move = _cameraMove;
            float drop = _drop;
            _moving = true;
            _zoomWanted = 0f;
            _zoomDone = 0f;

            try
            {
                LogCameraSpots();

                PlayerProfilePreview.ECameraViewType spot;
                if (!FindSpot(WholeBody ? PlayerProfilePreview.ECameraViewType.FullBody : _view, out spot))
                {
                    return;
                }

                Transform target = _preview.GetPositionNode(spot).CameraPosition;
                Vector3 home = target.localPosition;
                Quaternion homeRotation = target.localRotation;
                Task glide;
                if (WholeBody)
                {
                    target.localPosition = WHOLE_BODY_POSITION;
                    target.localRotation = Quaternion.identity;
                }
                else
                {
                    target.localPosition = home + new Vector3(0f, -drop, 0f);
                }
                try
                {
                    glide = _preview.ChangeCameraPosition(spot, 0.4f);
                }
                finally
                {
                    target.localPosition = home;
                    target.localRotation = homeRotation;
                }
                await glide;
            }
            catch (Exception error)
            {
                Plugin.Log.LogWarning("[ImprovedCustomizationUI] preview camera: " + error.Message);
            }
            finally
            {
                if (move == _cameraMove)
                {
                    _moving = false;
                }
            }
        }

        private bool FindSpot(PlayerProfilePreview.ECameraViewType view, out PlayerProfilePreview.ECameraViewType spot)
        {
            spot = view;
            if (_preview.GetPositionNode(view) != null)
            {
                return true;
            }

            if (view == PlayerProfilePreview.ECameraViewType.FullBody
                && _preview.GetPositionNode(PlayerProfilePreview.ECameraViewType.HalfBody) != null)
            {
                spot = PlayerProfilePreview.ECameraViewType.HalfBody;
                return true;
            }
            return false;
        }

        private void LogCameraSpots()
        {
            if (_loggedSpots)
            {
                return;
            }
            _loggedSpots = true;

            string spots = "";
            foreach (PlayerProfilePreview.PlayerProfilePreviewPosition spot in _preview._viewPorts)
            {
                spots += " " + spot.CameraType + "=" + (spot.CameraPosition != null ? spot.CameraPosition.localPosition.ToString("F2") : "none");
            }
            Plugin.Log.LogInfo("[ImprovedCustomizationUI] preview camera spots:" + spots);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _cameraMove++;
            if (_preview != null && _preview.DragTrigger != null)
            {
                _preview.DragTrigger.onDrag -= OnFreeLookDrag;
            }
            if (_scrollCatcher != null)
            {
                _scrollCatcher.Scrolled = null;
            }
        }
    }
}
