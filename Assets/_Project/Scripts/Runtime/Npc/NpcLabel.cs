using TMPro;
using UnityEngine;

namespace SaksiTerakhir.Npc
{
    [DisallowMultipleComponent]
    public sealed class NpcLabel : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text speechText;
        [SerializeField, Min(1f)] private float visibleDistance = 14f;
        private Camera viewingCamera;
        private float speechUntil;
        private float nextCameraCheck;

        public void Configure(TMP_Text nameLabel, TMP_Text speechLabel)
        {
            nameText = nameLabel;
            speechText = speechLabel;
        }

        public void SetName(string value)
        {
            if (nameText != null) nameText.text = value;
        }

        public void Say(string value, float seconds)
        {
            if (speechText != null) speechText.text = value;
            speechUntil = Time.time + seconds;
        }

        public void ClearSpeech()
        {
            speechUntil = 0f;
            if (speechText != null) speechText.text = string.Empty;
        }

        private void LateUpdate()
        {
            if ((viewingCamera == null || !viewingCamera.isActiveAndEnabled) && Time.time >= nextCameraCheck)
            {
                viewingCamera = Camera.main;
                nextCameraCheck = Time.time + 1f;
            }
            bool visible = viewingCamera != null && viewingCamera.isActiveAndEnabled
                && (viewingCamera.transform.position - transform.position).sqrMagnitude <= visibleDistance * visibleDistance;
            if (nameText != null && nameText.enabled != visible) nameText.enabled = visible;
            bool speaking = visible && Time.time < speechUntil;
            if (speechText != null && speechText.enabled != speaking) speechText.enabled = speaking;
            if (visible) transform.rotation = viewingCamera.transform.rotation;
        }
    }
}
