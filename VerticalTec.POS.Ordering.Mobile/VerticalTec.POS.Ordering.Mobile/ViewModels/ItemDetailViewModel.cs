using System;

using VerticalTec.POS.Ordering.Mobile.Models;

namespace VerticalTec.POS.Ordering.Mobile.ViewModels
{
    public class ItemDetailViewModel : BaseViewModel
    {
        public Item Item { get; set; }
        public ItemDetailViewModel(Item item = null)
        {
            Title = item?.Text;
            Item = item;
        }
    }
}
