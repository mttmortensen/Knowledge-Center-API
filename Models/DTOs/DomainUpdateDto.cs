namespace Knowledge_Center_API.Models.DTOs
{
    /*
     * PUT /api/domains
     * 
     * This DTO is going to be used in only updating
     * A Domain as we don't need update all the 
     * name/values.
     */
    public class DomainUpdateDto
    {
        public int DomainId { get; set; }
        public string? DomainName { get; set; }
        public string? DomainDescription { get; set; }
        public string? DomainStatus { get; set; }
    }
}
