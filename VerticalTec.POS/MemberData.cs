using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace VerticalTec.POS
{
    public class MemberData
    {
        [JsonProperty("memberId")]
        public int MemberId { get; set; }
        [JsonProperty("memberCode")]
        public string MemberCode { get; set; }
        [JsonProperty("memberGroupId")]
        public int MemberGroupId { get; set; }
        [JsonProperty("groupName")]
        public string GroupName { get; set; }
        [JsonProperty("gender")]
        public int Gender { get; set; }
        [JsonProperty("title")]
        public string Title { get; set; }
        [JsonProperty("firstName")]
        public string FirstName { get; set; }
        [JsonProperty("middleName")]
        public string MiddleName { get; set; }
        [JsonProperty("lastName")]
        public string LastName { get; set; }
        [JsonProperty("address1")]
        public string Address1 { get; set; }
        [JsonProperty("address2")]
        public string Address2 { get; set; }
        [JsonProperty("city")]
        public string City { get; set; }
        [JsonProperty("zipCode")]
        public string ZipCode { get; set; }
        [JsonProperty("provinceId")]
        public int ProvinceId { get; set; }
        [JsonProperty("provinceName")]
        public string ProvinceName { get; set; }
        [JsonProperty("countryId")]
        public int CountryId { get; set; }
        [JsonProperty("countryName")]
        public string CountryName { get; set; }
        [JsonProperty("phoneNo")]
        public string PhoneNo { get; set; }
        [JsonProperty("mobileNo")]
        public string MobileNo { get; set; }
        [JsonProperty("email")]
        public string Email { get; set; }
        [JsonProperty("birthDay")]
        public string Birthday { get; set; }
        [JsonProperty("idCardType")]
        public int IdCardType { get; set; }
        [JsonProperty("idCardNo")]
        public string IdCardNo { get; set; }
        [JsonProperty("passportNo")]
        public string PassportNo { get; set; }
        [JsonProperty("imgUrl")]
        public string ImgUrl { get; set; }

        [IgnoreDataMember]
        public string MemberFullName
        {
            get => $"{FirstName} {LastName}";
        }
    }
}
