using System;
using System.Collections;
using UnityEngine;

namespace Voxels.GameLogic.PlayerController
{
    [Serializable]
    public class FOVKick
    {
        [HideInInspector] public float originalFov;     // the original fov
        public float FOVIncrease = 3f;                  // the amount the field of view increases when going into a run
        public float TimeToIncrease = 1f;               // the amount of time the field of view will increase over
        public float TimeToDecrease = 1f;               // the amount of time the field of view will take to return to its original size
        public AnimationCurve IncreaseCurve;

        Camera _camera;                           // optional camera setup, if null the main camera will be used

        public void Setup(Camera camera)
        {
            CheckStatus(camera);

            _camera = camera;
            originalFov = camera.fieldOfView;
        }

        public IEnumerator FOVKickUp()
        {
            float time = Mathf.Abs((_camera.fieldOfView - originalFov)/FOVIncrease);
            while (time < TimeToIncrease)
            {
                _camera.fieldOfView = originalFov + (IncreaseCurve.Evaluate(time/TimeToIncrease)*FOVIncrease);
                time += Time.deltaTime;
                yield return new WaitForEndOfFrame();
            }
        }

        public IEnumerator FOVKickDown()
        {
            float t = Mathf.Abs((_camera.fieldOfView - originalFov)/FOVIncrease);
            while (t > 0)
            {
                _camera.fieldOfView = originalFov + (IncreaseCurve.Evaluate(t/TimeToDecrease)*FOVIncrease);
                t -= Time.deltaTime;
                yield return new WaitForEndOfFrame();
            }
            //make sure that fov returns to the original size
            _camera.fieldOfView = originalFov;
        }

        void CheckStatus(Camera camera)
        {
            if (camera == null)
                throw new Exception("FOVKick camera is null, please supply the camera to the constructor");

            if (IncreaseCurve == null)
                throw new Exception("FOVKick Increase curve is null, please define the curve for the field of view kicks");
        }
    }
}
