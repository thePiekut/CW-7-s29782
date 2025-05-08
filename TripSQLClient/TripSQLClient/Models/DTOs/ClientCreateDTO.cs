using System.ComponentModel.DataAnnotations;

namespace TripSQLClient.Models.DTOs;

public class ClientCreateDTO
{
    [Required]
    [MaxLength(120)]
    public string FirstName { get; set; }

    [Required]
    [MaxLength(120)]
    public string LastName { get; set; }

    [Required]
    [EmailAddress]
    [MaxLength(120)]
    public string Email { get; set; }

    [Required]
    [MaxLength(120)]
    public string Telephone { get; set; }

    [Required]
    [StringLength(11, MinimumLength = 11)]
    [RegularExpression("^[0-9]*$")]
    public string Pesel { get; set; }
}