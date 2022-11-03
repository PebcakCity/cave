namespace cave.drivers.projector {

    /// <summary>
    /// Common projector interface, just what every projector we want to control is expected to be able to do.
    /// How the device driver achieves all of this is its own business.
    
    /// For devices like NEC projectors operating on native NEC protocol, it makes some sense to have an always on 
    /// socket that is constantly requesting status updates and keeping some internal state ready for querying,
    /// as much of the data we want to maintain is contained in a single response that must be parsed.
    /// Ex. "Basic information request" is supported on most if not all NEC projectors and tells us about power state,
    /// what input is selected, video/audio mute status, video signal type, etc.
    /// On the other hand, basic lamp information is retrieved by effectively 4 different commands - one gets the
    /// factory-assigned total lamp life, another gets how many seconds the lamp has been used for, another gets
    /// how many seconds are remaining (which can of course be calculated by simple subtraction), and another the
    /// remaining life in percentage (which can also be calculated).

    /// For PJLink protocol, it might make more sense not to keep an always-open connection, as each command tends
    /// to return only what was requested.
    /// </summary>
    public interface IProjector {
        public void PowerOn();
        public void PowerOff();
        public void SelectInput( object input );
        public void PowerOnAndSelectInput( object input ); /* Achieved by powering on, waiting a few seconds, then attempting input selection */
    }

}
