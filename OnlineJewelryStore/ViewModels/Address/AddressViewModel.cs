using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace OnlineJewelryStore.ViewModels.Address
{
    public class AddressViewModel
    {
        public int AddressID { get; set; }

        [Required(ErrorMessage = "Street address is required")]
        [StringLength(255, ErrorMessage = "Street address cannot exceed 255 characters")]
        [Display(Name = "Street Address")]
        public string StreetAddress { get; set; }

        [Required(ErrorMessage = "City is required")]
        [StringLength(100, ErrorMessage = "City cannot exceed 100 characters")]
        [Display(Name = "City")]
        public string City { get; set; }

        [StringLength(100, ErrorMessage = "State/Province cannot exceed 100 characters")]
        [Display(Name = "State/Province")]
        public string State { get; set; }

        [Required(ErrorMessage = "Postal code is required")]
        [StringLength(20, ErrorMessage = "Postal code cannot exceed 20 characters")]
        [Display(Name = "Postal Code")]
        public string PostalCode { get; set; }

        [Required(ErrorMessage = "Country is required")]
        [StringLength(100, ErrorMessage = "Country cannot exceed 100 characters")]
        [Display(Name = "Country")]
        public string Country { get; set; }

        [Required(ErrorMessage = "Phone number is required")]
        [StringLength(20, ErrorMessage = "Phone number cannot exceed 20 characters")]
        [Phone(ErrorMessage = "Invalid phone number format")]
        [Display(Name = "Phone Number")]
        public string Phone { get; set; }

        [Display(Name = "Set as Default Address")]
        public bool IsDefault { get; set; }

        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Full address string for display
        /// </summary>
        public string FullAddress
        {
            get
            {
                return $"{StreetAddress}, {City}, {State} {PostalCode}, {Country}";
            }
        }

        /// <summary>
        /// Short address for compact display
        /// </summary>
        public string ShortAddress
        {
            get
            {
                return $"{City}, {State}";
            }
        }
    }

    /// <summary>
    /// ViewModel cho danh sách addresses
    /// </summary>
    public class AddressListViewModel
    {
        public int AddressID { get; set; }
        public string StreetAddress { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string PostalCode { get; set; }
        public string Country { get; set; }
        public string Phone { get; set; }
        public bool IsDefault { get; set; }
        public DateTime CreatedAt { get; set; }

        public string FullAddress
        {
            get
            {
                return $"{StreetAddress}, {City}, {State} {PostalCode}, {Country}";
            }
        }
    }
}