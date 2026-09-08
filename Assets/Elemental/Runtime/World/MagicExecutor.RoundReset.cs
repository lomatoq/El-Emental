namespace Elemental.Runtime.World
{
    public sealed partial class MagicExecutor
    {
        public void CancelForArenaRestore()
        {
            CancelHeldEarthControl(); CancelVectorField(); CancelGravityWell();
            foreach (TerrainExtractionTransaction transaction in _pendingExtractions)
            {
                transaction.MarkFailed();
                if (transaction.Fragment == null) continue;
                transaction.Fragment.StopBendControl(); transaction.Fragment.gameObject.SetActive(false);
            }
            _pendingExtractions.Clear(); _heldFragment = null;
            _pendingExtractionCancel = _pendingExtractionRelease = false;
            _pendingExtractionCharge = _pendingReleaseCharge = 0; _pendingReleaseTick = 0;
        }
    }
}
