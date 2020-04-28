using System;
using UnityEngine;

namespace Voxels.GameLogic.PlayerController
{
    [Serializable]
    public class CurveControlledBob
    {
        public float HorizontalBobRange = 0.33f;
        public float VerticalBobRange = 0.33f;
        public AnimationCurve Bobcurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.5f, 1f),
                                                            new Keyframe(1f, 0f), new Keyframe(1.5f, -1f),
                                                            new Keyframe(2f, 0f)); // sin curve for head bob
        float _verticaltoHorizontalRatio = 1f;

        float _cyclePositionX;
        float _cyclePositionY;
        float m_BobBaseInterval;
        Vector3 m_OriginalCameraPosition;
        float _time;

        public void Setup(Camera camera, float bobBaseInterval)
        {
            m_BobBaseInterval = bobBaseInterval;
            m_OriginalCameraPosition = camera.transform.localPosition;

            // get the length of the curve in time
            _time = Bobcurve[Bobcurve.length - 1].time;
        }

        public Vector3 DoHeadBob(float speed)
        {
            float xPos = m_OriginalCameraPosition.x + (Bobcurve.Evaluate(_cyclePositionX) * HorizontalBobRange);
            float yPos = m_OriginalCameraPosition.y + (Bobcurve.Evaluate(_cyclePositionY) * VerticalBobRange);

            _cyclePositionX += speed * Time.deltaTime / m_BobBaseInterval;
            _cyclePositionY += speed * Time.deltaTime / m_BobBaseInterval * _verticaltoHorizontalRatio;

            if (_cyclePositionX > _time)
                _cyclePositionX = _cyclePositionX - _time;
            
            if (_cyclePositionY > _time)
                _cyclePositionY = _cyclePositionY - _time;

            return new Vector3(xPos, yPos, 0f);
        }
    }
}
