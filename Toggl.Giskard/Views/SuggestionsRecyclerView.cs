﻿using System;
using Android.Content;
using Android.Runtime;
using Android.Support.V7.Widget;
using Android.Util;
using MvvmCross.Droid.Support.V7.RecyclerView;
using Toggl.Giskard.Adapters;

namespace Toggl.Giskard.Views
{
    [Register("toggl.giskard.views.SuggestionsRecyclerView")]
    public sealed class SuggestionsRecyclerView : MvxRecyclerView
    {
        public SuggestionsRecyclerView(IntPtr javaReference, JniHandleOwnership transfer)
            : base(javaReference, transfer)
        {
        }

        public SuggestionsRecyclerView(Context context, IAttributeSet attrs)
            : this(context, attrs, 0)
        {
        }

        public SuggestionsRecyclerView(Context context, IAttributeSet attrs, int defStyle)
            : base(context, attrs, defStyle, new SuggestionsRecyclerAdapter())
        {
            SetLayoutManager(new LinearLayoutManager(context, LinearLayoutManager.Horizontal, false));
            var snapHelper = new SuggestionsRecyclerViewSnapHelper();
            snapHelper.AttachToRecyclerView(this);
        }
    }
}
