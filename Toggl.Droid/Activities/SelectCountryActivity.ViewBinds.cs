using Android.Support.V7.Widget;
using Android.Widget;
using Toggl.Core.UI.ViewModels;
using Toggl.Droid.Adapters;
using Toggl.Droid.ViewHolders.Country;

namespace Toggl.Droid.Activities
{
    public partial class SelectCountryActivity
    {
        private readonly SimpleAdapter<SelectableCountryViewModel> recyclerAdapter =
            new SimpleAdapter<SelectableCountryViewModel>(
                Resource.Layout.SelectCountryActivityCountryCell,
                CountrySelectionViewHolder.Create);

        private ImageView backImageView;
        private EditText filterEditText;
        private RecyclerView recyclerView;

        protected override void InitializeViews()
        {
            backImageView = FindViewById<ImageView>(Resource.Id.BackImageView);
            filterEditText = FindViewById<EditText>(Resource.Id.FilterEditText);
            recyclerView = FindViewById<RecyclerView>(Resource.Id.RecyclerView);

            recyclerView.SetLayoutManager(new LinearLayoutManager(this)
            {
                ItemPrefetchEnabled = true,
                InitialPrefetchItemCount = 4
            });
            recyclerView.SetAdapter(recyclerAdapter);
        }
    }
}
