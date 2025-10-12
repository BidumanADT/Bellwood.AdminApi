using System;
using System.Collections.Generic;

namespace BellwoodGlobal.Mobile.Models
{
    public sealed class QuoteDraft
    {
        public Passenger Booker { get; set; } = new();
        public Passenger Passenger { get; set; } = new();
        public List<string> AdditionalPassengers { get; set; } = new();
        public string VehicleClass { get; set; } = "Sedan";
        public DateTime PickupDateTime { get; set; }
        public string PickupLocation { get; set; } = "";
        public PickupStyle PickupStyle { get; set; } = PickupStyle.Curbside;
        public string? PickupSignText { get; set; }
        public PickupStyle? ReturnPickupStyle { get; set; }
        public string? ReturnPickupSignText { get; set; }
        public bool AsDirected { get; set; }
        public int? Hours { get; set; }
        public string? DropoffLocation { get; set; }
        public bool RoundTrip { get; set; }
        public DateTime? ReturnPickupTime { get; set; }
        public FlightInfo OutboundFlight { get; set; } = new();
        public FlightInfo? ReturnFlight { get; set; }
        public string? AdditionalRequest { get; set; }
        public string? AdditionalRequestOtherText { get; set; }

        // capacity inputs
        public int PassengerCount { get; set; }
        public int? CheckedBags { get; set; }
        public int? CarryOnBags { get; set; }
    }
}
