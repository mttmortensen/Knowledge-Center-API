namespace Knowledge_Center_API.Models.Seasons
{
    /*
     * PUT /api/seasons/{id}/domains
     *
     * Replaces the season's whole lineup. Adding a Focus domain past the second
     * returns 409 with a SeasonOverloadWarningDto instead of saving; resend the
     * same lineup with OverloadAck filled in to go ahead.
     */
    public class SeasonDomainsSetDto
    {
        public List<SeasonDomainInputDto> Domains { get; set; } = new();
        public SeasonOverloadAckDto? OverloadAck { get; set; }
    }
}
