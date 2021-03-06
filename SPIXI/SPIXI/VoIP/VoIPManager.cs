﻿using IXICore;
using IXICore.Meta;
using SPIXI.Interfaces;
using SPIXI.Meta;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Xamarin.Forms;

namespace SPIXI.VoIP
{

    public class VoIPManager
    {
        public static byte[] currentCallSessionId { get; private set; }
        public static Friend currentCallContact { get; private set; }
        public static bool currentCallAccepted { get; private set; }
        public static bool currentCallCalleeAccepted { get; private set; }
        public static string currentCallCodec { get; private set; }
        public static long currentCallStartedTime { get; private set; }

        static IAudioPlayer audioPlayer = null;
        static IAudioRecorder audioRecorder = null;

        static long lastPacketReceivedTime = 0;
        static Thread lastPacketReceivedCheckThread = null;

        static bool currentCallInitiator = false;

        public static bool isInitiated()
        {
            if(currentCallSessionId != null)
            {
                return true;
            }
            return false;
        }

        public static void initiateCall(Friend friend)
        {
            if (currentCallSessionId != null)
            {
                return;
            }

            DependencyService.Get<ISpixiPermissions>().requestAudioRecordingPermissions();

            currentCallSessionId = Guid.NewGuid().ToByteArray();
            currentCallContact = friend;
            currentCallCalleeAccepted = false;
            currentCallAccepted = true;
            currentCallCodec = null;
            currentCallInitiator = true;

            string codecs = String.Join("|", DependencyService.Get<ISpixiCodecInfo>().getSupportedAudioCodecs());

            FriendList.addMessageWithType(currentCallSessionId, FriendMessageType.voiceCall, friend.walletAddress, "", true, null, 0, false);
            StreamProcessor.sendAppRequest(friend, "spixi.voip", currentCallSessionId, Encoding.UTF8.GetBytes(codecs));
            ((SpixiContentPage)App.Current.MainPage.Navigation.NavigationStack.Last()).displayCallBar(currentCallSessionId, "Dialing " + friend.nickname + "...", 0);

            DependencyService.Get<IPowerManager>().AquireLock("partial");
            DependencyService.Get<IPowerManager>().AquireLock("wifi");
            DependencyService.Get<IPowerManager>().AquireLock("proximityScreenOff");
        }

        public static bool onReceivedCall(Friend friend, byte[] session_id, byte[] data)
        {
            if (currentCallSessionId != null)
            {
                if (!currentCallSessionId.SequenceEqual(session_id))
                {
                    StreamProcessor.sendAppRequestReject(friend, session_id);
                }
                return false;
            }

            currentCallSessionId = session_id;
            currentCallContact = friend;
            currentCallCalleeAccepted = true;
            currentCallAccepted = false;
            currentCallCodec = null;
            currentCallInitiator = false;

            var codec_service = DependencyService.Get<ISpixiCodecInfo>();

            string codecs_str = Encoding.UTF8.GetString(data);

            string[] codecs = codecs_str.Split(new string[] { "|" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var codec in codecs)
            {
                if (codec_service.isCodecSupported(codec))
                {
                    currentCallCodec = codec;
                    break;
                }
            }
            if (currentCallCodec == null)
            {
                Logging.error("Unsupported audio codecs: " + codecs_str);
                endVoIPSession();
                return false;
            }
            DependencyService.Get<IPowerManager>().AquireLock("partial");
            DependencyService.Get<IPowerManager>().AquireLock("wifi");
            DependencyService.Get<IPowerManager>().AquireLock("proximityScreenOff");
            return true;
        }

        private static void startVoIPSession()
        {
            try
            {
                audioPlayer = DependencyService.Get<IAudioPlayer>(DependencyFetchTarget.NewInstance);
                audioPlayer.start(currentCallCodec);

                audioRecorder = DependencyService.Get<IAudioRecorder>(DependencyFetchTarget.NewInstance);
                audioRecorder.start(currentCallCodec);
                audioRecorder.setOnSoundDataReceived((data) =>
                {
                    StreamProcessor.sendAppData(currentCallContact, currentCallSessionId, data);
                });
                currentCallStartedTime = Clock.getTimestamp();
                startLastPacketReceivedCheck();
            }
            catch(Exception e)
            {
                Logging.error("Exception occured while starting VoIP session: " + e);
                endVoIPSession();
            }
        }

        private static void endVoIPSession()
        {
            try
            {
                if (audioPlayer != null)
                {
                    audioPlayer.Dispose();
                    audioPlayer = null;
                }
            }
            catch (Exception e)
            {
                audioPlayer = null;
                Logging.error("Exception occured in endVoIPSession 1: " + e);
            }

            try
            {
                if (audioRecorder != null)
                {
                    audioRecorder.Dispose();
                    audioRecorder = null;
                }
            }
            catch (Exception e)
            {
                audioRecorder = null;
                Logging.error("Exception occured in endVoIPSession 2: " + e);
            }

            currentCallContact.endCall(currentCallSessionId, currentCallAccepted && currentCallCalleeAccepted, Clock.getTimestamp() - currentCallStartedTime, currentCallInitiator);

            currentCallSessionId = null;
            currentCallContact = null;
            currentCallCalleeAccepted = false;
            currentCallAccepted = false;
            currentCallCodec = null;
            currentCallStartedTime = 0;
            lastPacketReceivedTime = 0;
            if (lastPacketReceivedCheckThread != null)
            {
                try
                {
                    lastPacketReceivedCheckThread.Abort();
                }
                catch (Exception)
                {
                }
                lastPacketReceivedCheckThread = null;
            }

            try
            {
                DependencyService.Get<IPowerManager>().ReleaseLock("partial");
                DependencyService.Get<IPowerManager>().ReleaseLock("wifi");
                DependencyService.Get<IPowerManager>().ReleaseLock("proximityScreenOff");
            }
            catch (Exception e)
            {
                Logging.error("Exception occured in endVoIPSession 3: " + e);
            }
            ((SpixiContentPage)App.Current.MainPage.Navigation.NavigationStack.Last()).hideCallBar();
        }

        public static void acceptCall(byte[] session_id)
        {
            if (!hasSession(session_id))
            {
                return;
            }

            if (currentCallAccepted)
            {
                return;
            }

            DependencyService.Get<ISpixiPermissions>().requestAudioRecordingPermissions();

            currentCallAccepted = true;
            StreamProcessor.sendAppRequestAccept(currentCallContact, session_id, Encoding.UTF8.GetBytes(currentCallCodec));
            startVoIPSession();
            ((SpixiContentPage)App.Current.MainPage.Navigation.NavigationStack.Last()).displayCallBar(currentCallSessionId, "In Call - " + currentCallContact.nickname, currentCallStartedTime);
        }

        public static void onAcceptedCall(byte[] session_id, byte[] data)
        {
            if (!hasSession(session_id))
            {
                return;
            }

            if(currentCallCalleeAccepted)
            {
                return;
            }

            currentCallCodec = Encoding.UTF8.GetString(data);
            currentCallCalleeAccepted = true;
            startVoIPSession();
            ((SpixiContentPage)App.Current.MainPage.Navigation.NavigationStack.Last()).displayCallBar(currentCallSessionId, "In Call - " + currentCallContact.nickname, currentCallStartedTime);
        }

        public static void rejectCall(byte[] session_id)
        {
            if (!hasSession(session_id))
            {
                return;
            }
            StreamProcessor.sendAppRequestReject(currentCallContact, session_id);
            endVoIPSession();
        }

        public static void onRejectedCall(byte[] session_id)
        {
            if (!hasSession(session_id))
            {
                return;
            }
            ((SpixiContentPage)App.Current.MainPage.Navigation.NavigationStack.Last()).hideCallBar();
            endVoIPSession();
        }

        public static void hangupCall(byte[] session_id)
        {
            if (session_id == null)
            {
                session_id = currentCallSessionId;
            }
            StreamProcessor.sendAppEndSession(currentCallContact, session_id);
            ((SpixiContentPage)App.Current.MainPage.Navigation.NavigationStack.Last()).hideCallBar();
            endVoIPSession();
        }

        public static void onHangupCall(byte[] session_id)
        {
            if (!hasSession(session_id))
            {
                return;
            }
            ((SpixiContentPage)App.Current.MainPage.Navigation.NavigationStack.Last()).hideCallBar();
            endVoIPSession();
        }

        public static void onData(byte[] session_id, byte[] data)
        {
            if (!hasSession(session_id))
            {
                return;
            }
            if (audioPlayer != null)
            {
                audioPlayer.write(data, 0, data.Length);
                lastPacketReceivedTime = Clock.getTimestamp();
            }
        }

        public static bool hasSession(byte[] session_id)
        {
            if(currentCallSessionId != null && session_id != null && currentCallSessionId.SequenceEqual(session_id))
            {
                return true;
            }
            return false;
        }

        private static void startLastPacketReceivedCheck()
        {
            lastPacketReceivedTime = Clock.getTimestamp();
            if(lastPacketReceivedCheckThread != null)
            {
                try
                {
                    lastPacketReceivedCheckThread.Abort();
                }catch(Exception)
                { 
                }
                lastPacketReceivedCheckThread = null;
            }
            lastPacketReceivedCheckThread = new Thread(lastPacketReceivedCheck);
            lastPacketReceivedCheckThread.IsBackground = true;
            lastPacketReceivedCheckThread.Start();
        }

        private static void lastPacketReceivedCheck()
        {
            while(currentCallStartedTime != 0 && lastPacketReceivedTime + 10 > Clock.getTimestamp())
            {
                Thread.Sleep(1000);
            }
            lastPacketReceivedCheckThread = null;
            endVoIPSession();
        }
    }
}
