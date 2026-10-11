using System;
using UnityEngine;

namespace SaksiTerakhir.Story
{
    public enum GrandfatherTopic { None, Introduction, GiriawasReport, NightTrail, WaitUntilMorning, GuestEtiquette }
    public enum GiriawasPlanSource { Unresolved, RegionalMap, GrandfatherReport }

    [Serializable]
    public sealed class FuelStationProgress
    {
        public const float FreeTimeSeconds = 150f;
        [SerializeField] private bool explorationStarted;
        [SerializeField] private float elapsedSeconds;
        [SerializeField] private bool ready;
        [SerializeField] private bool phoneCall;
        [SerializeField] private bool callDelivered;
        [SerializeField] private int grandfatherNextLine;
        [SerializeField] private GrandfatherTopic grandfatherTopic;
        [SerializeField] private bool photoTaken;
        [SerializeField] private GiriawasPlanSource planSource;
        [SerializeField] private bool departed;
        [SerializeField] private bool complete;

        public bool ExplorationStarted => explorationStarted;
        public float ElapsedSeconds => elapsedSeconds;
        public bool TankFull => explorationStarted;
        public int SpareFuelCans => ready ? 2 : 0;
        public bool ReceiptReceived => ready;
        public bool Ready => ready;
        public bool PhoneCall => phoneCall;
        public bool CallDelivered => callDelivered;
        public int GrandfatherNextLine => grandfatherNextLine;
        public GrandfatherTopic GrandfatherTopic => grandfatherTopic;
        public bool ReportHeard => grandfatherTopic >= GrandfatherTopic.GiriawasReport;
        public bool NightAdviceHeard => grandfatherTopic >= GrandfatherTopic.NightTrail;
        public bool MorningAdviceHeard => grandfatherTopic >= GrandfatherTopic.WaitUntilMorning;
        public bool DirectionsComplete => grandfatherTopic == GrandfatherTopic.GuestEtiquette;
        public bool PhotoTaken => photoTaken;
        public GiriawasPlanSource PlanSource => planSource;
        public bool GrandfatherInformationShared => planSource == GiriawasPlanSource.GrandfatherReport;
        public bool Departed => departed;
        public bool Complete => complete;

        public bool BeginExploration()
        {
            if (explorationStarted) return false;
            explorationStarted = true;
            return true;
        }

        // The caller supplies gameplay time, so paused menus never advance this clock.
        public bool AdvanceTime(float seconds, float colleagueDistance)
        {
            if (!explorationStarted || ready || departed || !Finite(seconds) || seconds <= 0f
                || !Finite(colleagueDistance) || colleagueDistance < 0f) return false;
            elapsedSeconds = Mathf.Min(FreeTimeSeconds, elapsedSeconds + seconds);
            if (elapsedSeconds < FreeTimeSeconds) return false;
            ready = true;
            phoneCall = colleagueDistance >= 10f;
            return true;
        }

        public bool MarkCallDelivered()
        {
            if (!ready || callDelivered) return false;
            callDelivered = true;
            return true;
        }

        public bool RecordGrandfatherLine(int completedLine, GrandfatherTopic heardTopic)
        {
            if (!explorationStarted || departed || completedLine != grandfatherNextLine
                || !Enum.IsDefined(typeof(GrandfatherTopic), heardTopic)
                || heardTopic < grandfatherTopic) return false;
            grandfatherNextLine++;
            grandfatherTopic = heardTopic;
            return true;
        }

        public bool TakePhoto()
        {
            if (!explorationStarted || departed || photoTaken) return false;
            photoTaken = true;
            return true;
        }

        public bool ChooseDestination()
        {
            if (!ready || departed || planSource != GiriawasPlanSource.Unresolved) return false;
            planSource = ReportHeard ? GiriawasPlanSource.GrandfatherReport : GiriawasPlanSource.RegionalMap;
            return true;
        }

        public bool Depart()
        {
            if (!ready || departed || planSource == GiriawasPlanSource.Unresolved) return false;
            departed = true;
            return true;
        }

        public bool Finish()
        {
            if (!departed || complete) return false;
            complete = true;
            return true;
        }

        public bool Validate(out string error)
        {
            if (!Finite(elapsedSeconds) || elapsedSeconds < 0f || elapsedSeconds > FreeTimeSeconds
                || !Enum.IsDefined(typeof(GrandfatherTopic), grandfatherTopic)
                || !Enum.IsDefined(typeof(GiriawasPlanSource), planSource)
                || grandfatherNextLine < 0 || grandfatherNextLine > 500)
                error = "Invalid fuel-station time or conversation state.";
            else if ((!explorationStarted && (elapsedSeconds != 0f || ready || grandfatherNextLine > 0 || photoTaken))
                || ready != (explorationStarted && elapsedSeconds >= FreeTimeSeconds)
                || (!ready && (phoneCall || callDelivered || planSource != GiriawasPlanSource.Unresolved))
                || (grandfatherNextLine == 0 && grandfatherTopic != GrandfatherTopic.None)
                || (planSource == GiriawasPlanSource.GrandfatherReport && !ReportHeard)
                || (departed && (!ready || planSource == GiriawasPlanSource.Unresolved))
                || (complete && !departed))
                error = "Fuel-station choices conflict with their prerequisites.";
            else { error = string.Empty; return true; }
            return false;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
