using System;
using FullThrottle.SacredCore.Vehicle;
using TMPro;
using UnityEngine;

namespace FullThrottle.SacredCore.Race
{
    [DefaultExecutionOrder(-150)]
    public sealed class FTOutrunChallengeDirector : MonoBehaviour
    {
        private enum ChallengeState
        {
            Searching,
            Prompt,
            Active,
            Won,
            Lost
        }

        [SerializeField] private FTVehicleTelemetry playerTelemetry;
        [SerializeField] private Transform playerTransform;
        [SerializeField] private KeyCode challengeKey = KeyCode.Return;
        [SerializeField] private TMP_Text promptText;
        [SerializeField] private bool useFallbackOnGuiPrompt = true;
        [SerializeField] private bool autoFindPlayer = true;
        [SerializeField] private float scanInterval = 0.35f;
        [SerializeField] private float defaultPromptRange = 16f;
        [SerializeField] private float defaultWinLeadMeters = 1000f;
        [SerializeField] private float defaultLoseLeadMeters = 1000f;
        [SerializeField] private float leaderSwitchMeters = 8f;
        [SerializeField] private float finishMessageSeconds = 4f;

        private FTOutrunRivalDriver[] rivals = Array.Empty<FTOutrunRivalDriver>();
        private FTOutrunRivalDriver promptedRival;
        private FTOutrunRivalDriver activeRival;
        private ChallengeState state;
        private float nextScanTime;
        private float finishTimer;
        private string currentPromptMessage = string.Empty;
        private bool currentPromptVisible;
        private bool playerIsLeader;
        private bool hasEstablishedLeader;

        public bool IsChallengeActive => state == ChallengeState.Active;
        public FTOutrunRivalDriver ActiveRival => activeRival;

        private void Awake()
        {
            ResolvePlayer();
            ScanRivals();
            SetPrompt(string.Empty, false);
        }

        private void Update()
        {
            ResolvePlayer();

            if (Time.unscaledTime >= nextScanTime)
            {
                nextScanTime = Time.unscaledTime + Mathf.Max(0.1f, scanInterval);
                ScanRivals();
            }

            switch (state)
            {
                case ChallengeState.Active:
                    UpdateActiveChallenge();
                    break;
                case ChallengeState.Won:
                case ChallengeState.Lost:
                    UpdateFinishMessage();
                    break;
                default:
                    UpdatePromptSearch();
                    break;
            }
        }

        private void OnGUI()
        {
            if (!useFallbackOnGuiPrompt || promptText != null || !currentPromptVisible || string.IsNullOrWhiteSpace(currentPromptMessage))
            {
                return;
            }

            GUIStyle style = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 24,
                fontStyle = FontStyle.Bold
            };
            style.normal.textColor = new Color(1f, 0.55f, 0.08f, 1f);

            float width = Mathf.Min(720f, Screen.width - 80f);
            Rect rect = new Rect((Screen.width - width) * 0.5f, Screen.height * 0.18f, width, 52f);
            GUI.Box(rect, currentPromptMessage, style);
        }

        public void BindPromptText(TMP_Text text)
        {
            promptText = text;
            SetPrompt(string.Empty, false);
        }

        private void UpdatePromptSearch()
        {
            promptedRival = FindPromptRival();
            if (promptedRival == null)
            {
                state = ChallengeState.Searching;
                SetPrompt(string.Empty, false);
                return;
            }

            state = ChallengeState.Prompt;
            string rivalName = promptedRival.Definition != null ? promptedRival.Definition.displayName : promptedRival.name;
            SetPrompt($"PRESS ENTER TO OUTRUN {rivalName.ToUpperInvariant()}", true);

            if (Input.GetKeyDown(challengeKey))
            {
                BeginChallenge(promptedRival);
            }
        }

        private void BeginChallenge(FTOutrunRivalDriver rival)
        {
            if (rival == null || playerTransform == null)
            {
                return;
            }

            activeRival = rival;
            promptedRival = null;
            state = ChallengeState.Active;
            hasEstablishedLeader = true;
            playerIsLeader = IsAheadOf(playerTransform, activeRival.transform, leaderSwitchMeters);
            activeRival.SetOutrunChaseTarget(playerIsLeader ? playerTransform : null);
            string rivalName = activeRival.Definition != null ? activeRival.Definition.displayName : activeRival.name;
            string leader = playerIsLeader ? "player" : rivalName;
            SetPrompt($"OUTRUN: OPEN A {ResolveWinLead(activeRival):0}M GAP", true);
            Debug.Log($"[SacredCore] Outrun started against '{rivalName}'. initialLeader={leader}, playerWinGap={ResolveWinLead(activeRival):0}m, rivalWinGap={ResolveLoseLead(activeRival):0}m.");
        }

        private void UpdateActiveChallenge()
        {
            if (activeRival == null || playerTransform == null)
            {
                FailChallenge("rival lost");
                return;
            }

            UpdateLeadership();

            float separation = CalculateHorizontalSeparation(playerTransform, activeRival.transform);
            float winLead = ResolveWinLead(activeRival);
            float loseLead = ResolveLoseLead(activeRival);

            activeRival.SetOutrunChaseTarget(playerIsLeader ? playerTransform : null);

            if (playerIsLeader && separation >= winLead)
            {
                WinChallenge();
                return;
            }

            if (!playerIsLeader && separation >= loseLead)
            {
                FailChallenge("rival pulled away");
                return;
            }

            if (playerIsLeader)
            {
                SetPrompt($"LEAD {separation:000} / {winLead:0000}M", true);
            }
            else
            {
                SetPrompt($"CATCH {separation:000} / {loseLead:0000}M", true);
            }
        }

        private void WinChallenge()
        {
            string rewardId = activeRival != null && activeRival.Definition != null ? activeRival.Definition.rewardId : "pending_reward";
            string rivalName = activeRival != null && activeRival.Definition != null ? activeRival.Definition.displayName : "rival";
            state = ChallengeState.Won;
            finishTimer = finishMessageSeconds;
            SetPrompt($"OUTRUN WON - UNLOCK {rewardId.ToUpperInvariant()}", true);
            Debug.Log($"[SacredCore] Outrun won against '{rivalName}'. reward={rewardId}.");
            if (activeRival != null)
            {
                activeRival.SetOutrunChaseTarget(null);
            }

            activeRival = null;
        }

        private void FailChallenge(string reason)
        {
            string rivalName = activeRival != null && activeRival.Definition != null ? activeRival.Definition.displayName : "rival";
            state = ChallengeState.Lost;
            finishTimer = finishMessageSeconds;
            SetPrompt("OUTRUN LOST", true);
            Debug.Log($"[SacredCore] Outrun lost against '{rivalName}'. reason={reason}.");
            if (activeRival != null)
            {
                activeRival.SetOutrunChaseTarget(null);
            }

            activeRival = null;
        }

        private void UpdateFinishMessage()
        {
            finishTimer -= Time.deltaTime;
            if (finishTimer > 0f)
            {
                return;
            }

            state = ChallengeState.Searching;
            SetPrompt(string.Empty, false);
        }

        private FTOutrunRivalDriver FindPromptRival()
        {
            if (playerTransform == null || rivals == null)
            {
                return null;
            }

            FTOutrunRivalDriver best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < rivals.Length; i++)
            {
                FTOutrunRivalDriver rival = rivals[i];
                if (rival == null || !rival.isActiveAndEnabled)
                {
                    continue;
                }

                float range = rival.Definition != null ? rival.Definition.challengePromptRange : defaultPromptRange;
                float distance = Vector3.Distance(playerTransform.position, rival.transform.position);
                if (distance > range || distance >= bestDistance)
                {
                    continue;
                }

                Vector3 toRival = rival.transform.position - playerTransform.position;
                toRival.y = 0f;
                float forwardDot = Vector3.Dot(playerTransform.forward, toRival.normalized);
                if (forwardDot < -0.35f)
                {
                    continue;
                }

                best = rival;
                bestDistance = distance;
            }

            return best;
        }

        private void ResolvePlayer()
        {
            if (!autoFindPlayer)
            {
                return;
            }

            if (playerTelemetry == null)
            {
                FTVehicleTelemetry[] vehicles = FindObjectsByType<FTVehicleTelemetry>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                for (int i = 0; i < vehicles.Length; i++)
                {
                    if (vehicles[i] != null && vehicles[i].CompareTag("Player"))
                    {
                        playerTelemetry = vehicles[i];
                        break;
                    }
                }

                if (playerTelemetry == null && vehicles.Length > 0)
                {
                    playerTelemetry = vehicles[0];
                }
            }

            if (playerTransform == null && playerTelemetry != null)
            {
                playerTransform = playerTelemetry.transform;
                Debug.Log($"[SacredCore] Outrun player target={playerTransform.name}.");
            }
        }

        private void ScanRivals()
        {
            rivals = FindObjectsByType<FTOutrunRivalDriver>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        }

        private void SetPrompt(string message, bool visible)
        {
            currentPromptMessage = message;
            currentPromptVisible = visible;

            if (promptText == null)
            {
                return;
            }

            promptText.gameObject.SetActive(visible);
            if (visible)
            {
                promptText.text = message;
            }
        }

        private void UpdateLeadership()
        {
            if (activeRival == null || playerTransform == null)
            {
                return;
            }

            bool playerAhead = IsAheadOf(playerTransform, activeRival.transform, leaderSwitchMeters);
            bool rivalAhead = IsAheadOf(activeRival.transform, playerTransform, leaderSwitchMeters);

            if (!hasEstablishedLeader)
            {
                hasEstablishedLeader = true;
                playerIsLeader = playerAhead && !rivalAhead;
                return;
            }

            if (playerAhead && !playerIsLeader)
            {
                playerIsLeader = true;
                Debug.Log("[SacredCore] Outrun leader changed: player now leads.");
            }
            else if (rivalAhead && playerIsLeader)
            {
                playerIsLeader = false;
                Debug.Log("[SacredCore] Outrun leader changed: rival now leads.");
            }
        }

        private static float CalculateHorizontalSeparation(Transform left, Transform right)
        {
            if (left == null || right == null)
            {
                return 0f;
            }

            Vector3 delta = left.position - right.position;
            delta.y = 0f;
            return delta.magnitude;
        }

        private static bool IsAheadOf(Transform candidateLeader, Transform candidateFollower, float switchMeters)
        {
            if (candidateLeader == null || candidateFollower == null)
            {
                return false;
            }

            Vector3 delta = candidateLeader.position - candidateFollower.position;
            delta.y = 0f;
            float distance = delta.magnitude;
            if (distance < Mathf.Max(1f, switchMeters))
            {
                return false;
            }

            Vector3 direction = delta / distance;
            float followerSeesLeaderAhead = Vector3.Dot(candidateFollower.forward, direction);
            float leaderMovingAwayFromFollower = Vector3.Dot(candidateLeader.forward, direction);
            float roughlySameDirection = Vector3.Dot(candidateLeader.forward, candidateFollower.forward);

            return followerSeesLeaderAhead > 0.2f
                || leaderMovingAwayFromFollower > 0.45f && roughlySameDirection > -0.35f;
        }

        private float ResolveWinLead(FTOutrunRivalDriver rival)
        {
            return rival != null && rival.Definition != null ? rival.Definition.leadDistanceToWin : defaultWinLeadMeters;
        }

        private float ResolveLoseLead(FTOutrunRivalDriver rival)
        {
            float configured = rival != null && rival.Definition != null ? rival.Definition.rivalLeadDistanceToLose : defaultLoseLeadMeters;
            return Mathf.Max(configured, ResolveWinLead(rival));
        }

    }
}
