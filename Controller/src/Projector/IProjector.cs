namespace cave.Controller.Projector {

    /// <summary>
    /// Basic functionality we expect from all projectors.
    /// </summary>
    public interface IProjector {
        public void PowerOn();
        public void PowerOff();
        public void SelectInput( object input );
        public void PowerOnAndSelectInput( object input ); /* Achieved by powering on, waiting a few seconds, then attempting input selection */
    }

}
