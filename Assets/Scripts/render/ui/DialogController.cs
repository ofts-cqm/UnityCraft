using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace render.ui
{
    public sealed class DialogController
    {
        private readonly GameObject _dialogOverlay;
        private readonly TextMeshProUGUI _dialogTitle;
        private readonly TextMeshProUGUI _dialogMessage;
        private readonly Button _dialogPrimary;
        private readonly TextMeshProUGUI _dialogPrimaryLabel;
        private readonly Button _dialogSecondary;
        private Action _dialogAction;
        
        public DialogController(Transform parent)
        {
            Image shade = MenuUiFactory.CreatePanel(parent, "Dialog Overlay", new Color(0, 0, 0, 0.78f));
            _dialogOverlay = shade.gameObject;
            MenuUiFactory.Stretch(shade.rectTransform);
            Image panel = MenuUiFactory.CreateThemedPanel(shade.transform, "Dialog", MenuPanelStyle.Modal);
            MenuUiFactory.SetAnchoredRect(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-370, -175), new Vector2(370, 175));

            _dialogTitle = MenuUiFactory.CreateText(panel.transform, "Title", string.Empty, 32);
            MenuUiFactory.SetAnchoredRect(_dialogTitle.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(30, -66), new Vector2(-30, -18));
            _dialogTitle.fontStyle = FontStyles.Bold;
            MenuUiFactory.ApplyTextStyle(_dialogTitle, MenuTextStyle.Title);
            _dialogMessage = MenuUiFactory.CreateText(panel.transform, "Message", string.Empty, 21);
            MenuUiFactory.SetAnchoredRect(_dialogMessage.rectTransform, new Vector2(0, 0), new Vector2(1, 1),
                new Vector2(45, 92), new Vector2(-45, -78));
            MenuUiFactory.ApplyTextStyle(_dialogMessage, MenuTextStyle.Body);

            _dialogSecondary = MenuUiFactory.CreateButton(panel.transform, "Secondary", "CANCEL", CloseDialog);
            MenuUiFactory.SetAnchoredRect(_dialogSecondary.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(0.5f, 0),
                new Vector2(45, 25), new Vector2(-8, 78));
            _dialogPrimary = MenuUiFactory.CreateButton(panel.transform, "Primary", "OK", InvokeDialogAction);
            MenuUiFactory.SetAnchoredRect(_dialogPrimary.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(1, 0),
                new Vector2(8, 25), new Vector2(-45, 78));
            _dialogPrimaryLabel = _dialogPrimary.GetComponentInChildren<TextMeshProUGUI>();
            _dialogOverlay.SetActive(false);
        }
        
        public void ShowDialog(string title, string message, string primary, Action action = null, string secondary = null)
        {
            _dialogTitle.text = title;
            _dialogMessage.text = message;
            _dialogPrimaryLabel.text = primary;
            _dialogAction = action;
            _dialogSecondary.gameObject.SetActive(!string.IsNullOrEmpty(secondary));
            if (!string.IsNullOrEmpty(secondary)) _dialogSecondary.GetComponentInChildren<TextMeshProUGUI>().text = secondary;
            RectTransform primaryRect = _dialogPrimary.GetComponent<RectTransform>();
            if (string.IsNullOrEmpty(secondary))
                MenuUiFactory.SetAnchoredRect(primaryRect, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-180, 25), new Vector2(180, 78));
            else
                MenuUiFactory.SetAnchoredRect(primaryRect, new Vector2(0.5f, 0), new Vector2(1, 0), new Vector2(8, 25), new Vector2(-45, 78));
            _dialogOverlay.SetActive(true);
        }

        private void InvokeDialogAction()
        {
            Action action = _dialogAction;
            CloseDialog();
            action?.Invoke();
        }

        private void CloseDialog()
        {
            _dialogOverlay.SetActive(false);
            _dialogAction = null;
        }
    }
}