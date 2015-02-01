using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpSenses.Gestures;
using SharpSenses.Poses;

namespace SharpSenses.RealSense {
    public class RealSenseCamera : Camera {
        private pxcmStatus NoError = pxcmStatus.PXCM_STATUS_NO_ERROR;
        private PXCMSession _session;
        private PXCMSenseManager _manager;
        private CancellationTokenSource _cancellationToken;

        public override int ResolutionWidth {
            get { return 1280; }
        }

        public override int ResolutionHeight {
            get { return 720; }
        }

        /// <summary>
        /// Gets current frame as an Image from camera.
        /// </summary>
        public Image CurrentFrameImage
        {
            get
            {
                lock (lfiSyncRoot)
                {
                    return this.lastFrameImage;
                }
            }
        }
        private Image lastFrameImage = null;
        private object lfiSyncRoot = new object();

        private void SaveFrameImage(PXCMFaceData faceData, PXCMHandData handData)
        {
            PXCMCapture.Sample sample = _manager.QuerySample();
            if (sample == null)
            {
                _manager.ReleaseFrame();
                return;
            }

            PXCMImage image = sample.color;
            PXCMImage.ImageData data;

            pxcmStatus status = image.AcquireAccess(PXCMImage.Access.ACCESS_READ, PXCMImage.PixelFormat.PIXEL_FORMAT_RGB32, out data);
            if (status < pxcmStatus.PXCM_STATUS_NO_ERROR)
            {
                throw new Exception(status.ToString());
            }

            // TODO: Can not understand why image is cropped?
            lock (lfiSyncRoot)
            {
                lastFrameImage = data.ToBitmap(0, ResolutionWidth, ResolutionHeight);
                image.ReleaseAccess(data);

                // Drawing debug information.
                try
                {
                    Color color = Color.FromArgb(0xff, 0xff, 0xff, 0x00);
                    SolidBrush brush = new SolidBrush(color);

                    using (Graphics graphics = Graphics.FromImage(lastFrameImage))
                    {
                        int debugMessagesCount = 0;
                        var debugMessage = new Action<string>((s) =>
                        {
                            graphics.DrawString(s, new Font("Arial", 15), brush, 10, 10 + debugMessagesCount++ * 20);
                        });

                        var drawPoint = new Action<PXCMPointF32>(p =>
                        {
                            if (2 < p.x && p.x < ResolutionWidth - 2 &&
                                2 < p.y && p.x < ResolutionHeight - 2)
                            {
                                graphics.FillEllipse(brush, p.x - 2, p.y - 2, 4, 4);
                            }
                        });

                        // Drawing face debug data.
                        var faces = faceData.QueryFaces();
                        if (faces.Length == 1)
                        {
                            var face = faces.First();
                            var faceLandmarks = face.QueryLandmarks();

                            PXCMFaceData.LandmarkPoint[] facePoints = null;
                            if (faceLandmarks != null && faceLandmarks.QueryPoints(out facePoints))
                            {
                                foreach (var point in facePoints)
                                {
                                    drawPoint(point.image);
                                }
                            }
                        }
                        else if (faces.Length == 0)
                        {

                            debugMessage("No faces detected");
                        }
                        else
                        {
                            debugMessage("More than one face on the camera");
                        }

                        // Drawing hands debug data.
                        int numHands = handData.QueryNumberOfHands();
                        if (numHands == 0)
                        {
                            debugMessage("No hands detected");
                        }
                        else
                        {
                            for (var i = 0; i < numHands; i++)
                            {
                                PXCMHandData.IHand hand;
                                pxcmStatus st = handData.QueryHandData(PXCMHandData.AccessOrderType.ACCESS_ORDER_BY_TIME, i, out hand);
                                if (st == pxcmStatus.PXCM_STATUS_NO_ERROR)
                                {
                                    var bbox = hand.QueryBoundingBoxImage();
                                    if (0 < bbox.x && bbox.x + bbox.w < ResolutionWidth &&
                                        0 < bbox.y && bbox.y + bbox.h < ResolutionHeight)
                                    {
                                        graphics.DrawRectangle(new Pen(brush), bbox.x, bbox.y, bbox.w, bbox.h);
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.Write(e);
                    lastFrameImage = null;
                }
            }
        }

        public int CyclePauseInMillis { get; set; }

        public RealSenseCamera(PXCMSession session = null) {
            _session = session ?? PXCMSession.CreateInstance();
            _manager = _session.CreateSenseManager();
            ConfigurePoses();
            ConfigureGestures();
            Debug.WriteLine("Visual Module");
            Debug.WriteLine("SDK Version {0}.{1}", _session.QueryVersion().major, _session.QueryVersion().minor);
        }

        private void ConfigureGestures() {
            GestureSlide.Configue(this, _gestures);
        }

        private void ConfigurePoses() {
            PosePeace.Configue(LeftHand, _poses);
            PosePeace.Configue(RightHand, _poses);
        }

        public override void Start() {
            _cancellationToken = new CancellationTokenSource();
            _manager.EnableHand();
            _manager.EnableFace();
            _manager.EnableEmotion();
            using (var handModule = _manager.QueryHand()) {
                using (var handConfig = handModule.CreateActiveConfiguration()) {
                    //handConfig.EnableAllAlerts();
                    int numGestures = handConfig.QueryGesturesTotalNumber();
                    for (int i = 0; i < numGestures; i++) {
                        string name;
                        handConfig.QueryGestureNameByIndex(i, out name);
                        Debug.WriteLine("Gestures: " + name);
                    }
                    handConfig.EnableAllGestures();
                    handConfig.SubscribeGesture(OnGesture);                    
                    handConfig.EnableTrackedJoints(true);
                    handConfig.ApplyChanges();
                }
            }

            var status = _manager.Init();
            if (status != NoError) {
                throw new CameraException(status.ToString());
            }
            Task.Factory.StartNew(Loop,
                                  TaskCreationOptions.LongRunning,
                                  _cancellationToken.Token);
        }

        private void Loop(object notUsed) {
            try {
                TryLoop();                
            }
            catch (Exception ex) {
                Debug.WriteLine("Loop error: " + ex);
                throw;
            }
        }

        private void TryLoop() {
            Debug.WriteLine("Loop started");
            var handModule = _manager.QueryHand();
            var faceModule = _manager.QueryFace();
            var handData = handModule.CreateOutput();
            var faceData = faceModule.CreateOutput();

            while (!_cancellationToken.IsCancellationRequested) {
                _manager.AcquireFrame(true);
                handData.Update();
                TrackHandAndFingers(LeftHand, handData, PXCMHandData.AccessOrderType.ACCESS_ORDER_LEFT_HANDS);
                TrackHandAndFingers(RightHand, handData, PXCMHandData.AccessOrderType.ACCESS_ORDER_RIGHT_HANDS);

                faceData.Update();
                TrackFace(faceData);
                TrackEmotions();

                SaveFrameImage(faceData, handData);

                _manager.ReleaseFrame();
                if (CyclePauseInMillis > 0) {
                    Thread.Sleep(CyclePauseInMillis);
                }
            }
            handData.Dispose();
            faceData.Dispose();
            handModule.Dispose();
            faceModule.Dispose();
        }

        private void TrackEmotions() {
            if (!Face.IsVisible) {
                return;
            }
            var emotionInfo = _manager.QueryEmotion();
            if (emotionInfo == null) {
                return;
            }
            PXCMEmotion.EmotionData[] allEmotions;
            emotionInfo.QueryAllEmotionData(0, out allEmotions);
            emotionInfo.Dispose();
            if (allEmotions == null) {
                return;
            }
            var emotions =
                allEmotions.Where(e => (int)e.eid > 0 && (int)e.eid <= 64 && e.intensity > 0.4).ToList();
            if (emotions.Any()) {
                var emotion = emotions.OrderByDescending(e => e.evidence).First();
                Face.FacialExpression = (FacialExpression)emotion.eid;
            }
            else {
                Face.FacialExpression = FacialExpression.None;
            }
            emotionInfo.Dispose();
        }

        private void TrackFace(PXCMFaceData faceData) {
            if (faceData.QueryNumberOfDetectedFaces() == 0) {
                Face.IsVisible = false;
                return;
            }
            Face.IsVisible = true;
            var face = faceData.QueryFaces().First();
            PXCMRectI32 rect;
            face.QueryDetection().QueryBoundingRect(out rect);
            var point = new Point3D(rect.x + rect.w /2, rect.y + rect.h /2);
            Face.Position = CreatePosition(point, new Point3D());

            PXCMFaceData.LandmarksData landmarksData = face.QueryLandmarks();
            if (landmarksData == null) {
                return;
            }
            PXCMFaceData.LandmarkPoint[] facePoints;
            landmarksData.QueryPoints(out facePoints);
            if(facePoints == null) {
                return;
            }
            foreach (var item in facePoints) {
                switch(item.source.alias) {
                    case PXCMFaceData.LandmarkType.LANDMARK_UPPER_LIP_CENTER:
                        Face.Mouth.Position = CreatePosition(ToPoint3D(item.image), ToPoint3D(item.world));
                        break;
                }
            }
        }

        private string _last = "";

        private void OnGesture(PXCMHandData.GestureData gesturedata) {
            string g = String.Format("Gesture: {0}-{1}-{2}",
                gesturedata.name,
                gesturedata.handId,
                gesturedata.state);
            if (_last == g) {
                return;
            }
            _last = g;
            Debug.WriteLine(g);
            switch (gesturedata.name) {
                case "wave":
                    _gestures.OnWave(new GestureEventArgs("wave"));
                    return;
            }
        }

        private void TrackHandAndFingers(Hand hand, PXCMHandData data, PXCMHandData.AccessOrderType label) {
            PXCMHandData.IHand handInfo;
            if (data.QueryHandData(label, 0, out handInfo) != NoError) {
                hand.IsVisible = false;
                return;
            }
            hand.IsVisible = true;
            SetHandOpenness(hand, handInfo);
            SetHandPosition(hand, handInfo);
            TrackIndex(hand.Index, handInfo);
            TrackMiddle(hand.Middle, handInfo);
            TrackRing(hand.Ring, handInfo);
            TrackPinky(hand.Pinky, handInfo);
            TrackThumb(hand.Thumb, handInfo);
        }

        private void SetHandPosition(Hand hand, PXCMHandData.IHand handInfo) {
            var imagePosition = ToPoint3D(handInfo.QueryMassCenterImage());
            var worldPosition = ToPoint3D(handInfo.QueryMassCenterWorld());
            hand.Position = CreatePosition(imagePosition, worldPosition);
        }

        private void SetHandOpenness(Hand hand, PXCMHandData.IHand handInfo) {
            int openness = handInfo.QueryOpenness();
            SetOpenness(hand, openness);
        }

        private void TrackIndex(Finger finger, PXCMHandData.IHand handInfo) {
            SetJointdata(handInfo, PXCMHandData.JointType.JOINT_INDEX_BASE, finger.BaseJoint);
            SetJointdata(handInfo, PXCMHandData.JointType.JOINT_INDEX_JT1, finger.FirstJoint);
            SetJointdata(handInfo, PXCMHandData.JointType.JOINT_INDEX_JT2, finger.SecondJoint);
            SetJointdata(handInfo, PXCMHandData.JointType.JOINT_INDEX_TIP, finger);
            SetFingerOpenness(finger, PXCMHandData.FingerType.FINGER_INDEX, handInfo);
        }

        private void TrackMiddle(Finger finger, PXCMHandData.IHand handInfo) {
            SetJointdata(handInfo, PXCMHandData.JointType.JOINT_MIDDLE_BASE, finger.BaseJoint);
            SetJointdata(handInfo, PXCMHandData.JointType.JOINT_MIDDLE_JT1, finger.FirstJoint);
            SetJointdata(handInfo, PXCMHandData.JointType.JOINT_MIDDLE_JT2, finger.SecondJoint);
            SetJointdata(handInfo, PXCMHandData.JointType.JOINT_MIDDLE_TIP, finger);
            SetFingerOpenness(finger, PXCMHandData.FingerType.FINGER_MIDDLE, handInfo);
        }

        private void TrackRing(Finger finger, PXCMHandData.IHand handInfo) {
            SetJointdata(handInfo, PXCMHandData.JointType.JOINT_RING_BASE, finger.BaseJoint);
            SetJointdata(handInfo, PXCMHandData.JointType.JOINT_RING_JT1, finger.FirstJoint);
            SetJointdata(handInfo, PXCMHandData.JointType.JOINT_RING_JT2, finger.SecondJoint);
            SetJointdata(handInfo, PXCMHandData.JointType.JOINT_RING_TIP, finger);
            SetFingerOpenness(finger, PXCMHandData.FingerType.FINGER_RING, handInfo);
        }

        private void TrackPinky(Finger finger, PXCMHandData.IHand handInfo) {
            SetJointdata(handInfo, PXCMHandData.JointType.JOINT_PINKY_BASE, finger.BaseJoint);
            SetJointdata(handInfo, PXCMHandData.JointType.JOINT_PINKY_JT1, finger.FirstJoint);
            SetJointdata(handInfo, PXCMHandData.JointType.JOINT_PINKY_JT2, finger.SecondJoint);
            SetJointdata(handInfo, PXCMHandData.JointType.JOINT_PINKY_TIP, finger);
            SetFingerOpenness(finger, PXCMHandData.FingerType.FINGER_PINKY, handInfo);
        }

        private void TrackThumb(Finger finger, PXCMHandData.IHand handInfo) {
            SetJointdata(handInfo, PXCMHandData.JointType.JOINT_THUMB_BASE, finger.BaseJoint);
            SetJointdata(handInfo, PXCMHandData.JointType.JOINT_THUMB_JT1, finger.FirstJoint);
            SetJointdata(handInfo, PXCMHandData.JointType.JOINT_THUMB_JT2, finger.SecondJoint);
            SetJointdata(handInfo, PXCMHandData.JointType.JOINT_THUMB_TIP, finger);
            SetFingerOpenness(finger, PXCMHandData.FingerType.FINGER_THUMB, handInfo);
        }

        private void SetFingerOpenness(Finger finger, PXCMHandData.FingerType fingerType, PXCMHandData.IHand handInfo) {
            PXCMHandData.FingerData fingerData;
            if (handInfo.QueryFingerData(fingerType, out fingerData) != NoError) {
                return;
            }
            SetOpenness(finger, fingerData.foldedness);            
        }

        private void SetJointdata(PXCMHandData.IHand handInfo, PXCMHandData.JointType jointType, Item joint) {
            PXCMHandData.JointData jointData;
            if (handInfo.QueryTrackedJoint(jointType, out jointData) != NoError) {
                joint.IsVisible = false;
                return;
            }
            SetVisibleJointPosition(joint, jointData);
        }

        private void SetOpenness(FlexiblePart part, int scaleZeroToHundred) {
            if (scaleZeroToHundred > 75) {
                part.IsOpen = true;
            }
            else if (scaleZeroToHundred < 35) {
                part.IsOpen = false;
            }
        }

        private void SetVisibleJointPosition(Item item, PXCMHandData.JointData jointData) {
            var imagePosition = ToPoint3D(jointData.positionImage);
            var worldPosition = ToPoint3D(jointData.positionWorld);
            item.IsVisible = true;
            item.Position = CreatePosition(imagePosition, worldPosition);
        }

        private Point3D ToPoint3D(PXCMPointF32 p) {
            return new Point3D(p.x, p.y);            
        }

        private Point3D ToPoint3D(PXCMPoint3DF32 p) {
            return new Point3D(p.x, p.y, p.z);
        }

        public override void Dispose() {
            _cancellationToken.Cancel();
            try {
                _manager.Dispose();
            }
            catch { }
            try {
                _session.Dispose();                
            }
            catch { }
        }
    }
}
