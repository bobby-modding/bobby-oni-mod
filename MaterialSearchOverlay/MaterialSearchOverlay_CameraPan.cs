namespace BobbyModding.MaterialSearchOverlay {
    /// <summary>
    /// Releases a movement key the camera controller still believes is held down.
    /// </summary>
    /// <remarks>
    /// The camera keeps one bool per pan direction and clears it only in the key up handler.
    /// While a text box owns the keyboard that handler ignores key events, so pressing "A"
    /// and then clicking into the search box loses the key up that arrives afterwards: the
    /// flag stays set and the camera slides that way for the rest of the session, even after
    /// the overlay closes. The flags are private fields, but the public
    /// <see cref="CameraController.DisableUserCameraControl"/> setter clears all of them when
    /// set to true, so flipping it on and straight back off releases a latched key without
    /// touching reflection and without leaving camera control disabled for anything else.
    /// </remarks>
    internal static class CameraPan {
        /// <summary>
        /// Drops any pan direction the camera is still treating as held. Does nothing while
        /// some other system has camera control disabled, since turning the flag back off
        /// would hand control to the player in the middle of a cutscene or victory screen.
        /// </summary>
        internal static void ReleaseHeldKeys() {
            var camera = CameraController.Instance;
            if (camera == null || camera.DisableUserCameraControl)
                return;
            camera.DisableUserCameraControl = true;
            camera.DisableUserCameraControl = false;
        }
    }
}
