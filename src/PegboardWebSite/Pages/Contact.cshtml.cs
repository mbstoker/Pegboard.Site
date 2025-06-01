using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Primitives;

namespace PegboardWebSite.Pages
{
    public class ContactModel : PageModel
    {
        public void OnGet()
        {
        }

        private string _text = "Blah";
        public string Text
        {
            get
            {
                return _text;
            }
            set
            {
                _text = value;
            }
        }
    }
}
