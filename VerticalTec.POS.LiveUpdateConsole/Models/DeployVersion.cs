using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using VerticalTec.POS.LiveUpdate;

namespace VerticalTec.POS.LiveUpdateConsole.Models
{
    public class DeployVersion
    {
        public int BrandId { get; set; }
        public int ShopId { get; set; }
        public string ProgramName { get; set; } = "vTec-ResPOS";
        public ProgramTypes ProgramId { get; set; } = LiveUpdate.ProgramTypes.Front;
        public VersionDeployBatchStatus BatchStatus { get; set; } = VersionDeployBatchStatus.InActivate;

        [Required(ErrorMessage = "Please input program version")]
        public string ProgramVersion { get; set; }
        [Required(ErrorMessage = "Please input google share file url")]
        public string FileUrl { get; set; }
        public bool AutoBackup { get; set; }

        public List<ProgramType> ProgramTypes { get; set; } = new List<ProgramType>(){
            new ProgramType()
                {
                    ProgramTypeId = LiveUpdate.ProgramTypes.Front,
                    ProgramName = "vTec-ResPOS"
                }
            };
    }
}
