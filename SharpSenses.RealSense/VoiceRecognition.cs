/*******************************************************************************

INTEL CORPORATION PROPRIETARY INFORMATION
This software is supplied under the terms of a license agreement or nondisclosure
agreement with Intel Corporation and may not be copied or disclosed except in
accordance with the terms of that agreement
Copyright(c) 2013-2014 Intel Corporation. All Rights Reserved.

*******************************************************************************/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace SharpSenses.RealSense
{
    /// <summary>
    /// Represents RealSense Voice Recognition module wrapper.
    /// </summary>
    public class VoiceRecognition
    {
        PXCMSession session;
        PXCMAudioSource source;
        PXCMSpeechRecognition sr;

        public VoiceRecognition(PXCMSession session = null)
        {
            this.session = session ?? PXCMSession.CreateInstance();
            Debug.WriteLine("Voice Recognition Module");
            Debug.WriteLine("SDK Version {0}.{1}", session.QueryVersion().major, session.QueryVersion().minor);
        }

        /// <summary>
        /// Invoked when full sentence is recognized.
        /// </summary>
        public event EventHandler<string> OnSentenceRecognition;
        /// <summary>
        /// Invoked when alert event is received from RealSense.
        /// </summary>
        public event EventHandler<PXCMSpeechRecognition.AlertType> OnRecognitionAlert;

        void OnRecognition(PXCMSpeechRecognition.RecognitionData data)
        {
            if (data.scores[0].label < 0)
            {
                string sentence = data.scores[0].sentence;
                Debug.WriteLine("Sentence recognized: " + sentence);
                if (OnSentenceRecognition != null)
                {
                    OnSentenceRecognition.Invoke(this, data.scores[0].sentence);
                }
            }
        }

        void OnAlert(PXCMSpeechRecognition.AlertData data)
        {
            Debug.WriteLine("Alert received: " + data.label.ToString());
            if (OnRecognitionAlert != null)
            {
                OnRecognitionAlert.Invoke(this, data.label);
            }
        }

        public void Stop()
        {
            if (sr != null)
            {
                sr.Dispose();
                sr = null;
            }
            if (source != null)
            {
                source.Dispose();
                source = null;
            }
        }

        public void Start()
        {
            /* Create the AudioSource instance */
            source = session.CreateAudioSource();

            if (source == null)
            {
                Stop();
                return;
            }

            /* Set audio volume to 0.2 */
            source.SetVolume(0.1f);

            /* Set Audio Source */
            source.ScanDevices();
            PXCMAudioSource.DeviceInfo dinfo;
            pxcmStatus sts = source.QueryDeviceInfo(0, out dinfo);
            if (sts != pxcmStatus.PXCM_STATUS_NO_ERROR)
            {
                throw new Exception(String.Format("Voice Recognition failed with code: {0}", sts));
            }
            source.SetDevice(dinfo);

            /* Set Module */
            PXCMSession.ImplDesc mdesc = new PXCMSession.ImplDesc();
            mdesc.iuid = PXCMSpeechRecognition.CUID;

            sts = session.CreateImpl<PXCMSpeechRecognition>(out sr);
            if (sts >= pxcmStatus.PXCM_STATUS_NO_ERROR)
            {
                sr.SetDictation();

                /* Initialization */
                Debug.WriteLine("Init Started");
                PXCMSpeechRecognition.Handler handler = new PXCMSpeechRecognition.Handler();
                handler.onRecognition = OnRecognition;
                handler.onAlert = OnAlert;

                sts = sr.StartRec(source, handler);
                if (sts >= pxcmStatus.PXCM_STATUS_NO_ERROR)
                {
                    Debug.WriteLine("Init OK");
                }
                else
                {
                    throw new Exception(String.Format("Voice Recognition failed with code: {0}", sts));
                }
            }
            else
            {
                throw new Exception(String.Format("Voice Recognition failed with code: {0}", sts));
            }
        }
    }
}
