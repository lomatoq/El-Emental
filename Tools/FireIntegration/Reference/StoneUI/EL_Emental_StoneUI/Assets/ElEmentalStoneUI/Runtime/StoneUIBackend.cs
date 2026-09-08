using UnityEngine;

namespace ElEmental.StoneUI
{
    /// <summary>Implement this one bridge against the CURRENT project, not a guessed transport API.</summary>
    public abstract class StoneUIBackend : MonoBehaviour
    {
        public StoneUIController view;
        protected virtual void OnEnable()
        {
            if(view==null)view=GetComponent<StoneUIController>();
            if(view==null){Debug.LogError("StoneUIBackend requires an assigned view.",this);return;}
            view.ActionRequested+=HandleAction;view.InputBlockChanged+=HandleInputBlock;view.SettingsChanged+=HandleSettings;
            HandleInputBlock(view.BlocksGameplayInput);
        }
        protected virtual void OnDisable()
        {
            if(view==null)return;
            view.ActionRequested-=HandleAction;view.InputBlockChanged-=HandleInputBlock;view.SettingsChanged-=HandleSettings;
        }
        protected abstract void HandleAction(string action,string payload);
        protected abstract void HandleInputBlock(bool blocked);
        protected abstract void HandleSettings(StoneUISettings settings);
        // Backend -> view: ApplyHud, SetRoom, SetLoading, SetMinimap, ShowResult,
        // ShowConnectionError and ShowScreen. This layer NEVER fabricates networking.
    }
}
