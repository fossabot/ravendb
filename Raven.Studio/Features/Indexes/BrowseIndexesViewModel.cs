﻿using Raven.Abstractions.Indexing;
using Raven.Studio.Infrastructure.Navigation;

namespace Raven.Studio.Features.Indexes
{
	using System.ComponentModel.Composition;
	using Caliburn.Micro;
	using Framework;
	using Framework.Extensions;
	using Messages;
	using Plugins;
	using Plugins.Database;

    [Export]
	[ExportDatabaseExplorerItem("Indexes", Index = 30)]
	public class BrowseIndexesViewModel : RavenScreen,
										  IHandle<IndexUpdated>
	{
		readonly IServer server;
		IndexDefinition activeIndex;
		object activeItem;

		[ImportingConstructor]
		public BrowseIndexesViewModel(IServer server, IEventAggregator events, NavigationService navigationService)
			: base(events, navigationService)
		{
			DisplayName = "Indexes";

			this.server = server;
			events.Subscribe(this);

			server.CurrentDatabaseChanged += delegate
			{
			    ActiveItem = null;
				if(Indexes != null) Indexes.Clear();
			};
		}

		protected override void OnInitialize()
		{
			Indexes = new BindablePagedQuery<IndexDefinition>((start, pageSize) =>
			{
				using(var session = server.OpenSession())
				return session.Advanced.AsyncDatabaseCommands
					.GetIndexesAsync(start, pageSize);
			});
		}

		protected override void OnActivate()
		{
			BeginRefreshIndexes();
		}

		public void CreateNewIndex()
		{
			ActiveItem = new EditIndexViewModel(new IndexDefinition(), server, Events, NavigationService);
		}

		void BeginRefreshIndexes()
		{
			WorkStarted("retrieving indexes");
			using (var session = server.OpenSession())
				session.Advanced.AsyncDatabaseCommands
					.GetStatisticsAsync()
					.ContinueWith(
						_ =>
							{
								WorkCompleted("retrieving indexes");
								RefreshIndexes(_.Result.CountOfIndexes);
							},
						faulted =>
							{
								WorkCompleted("retrieving indexes");
							}
						);
		}

		public BindablePagedQuery<IndexDefinition> Indexes { get; private set; }

		public IndexDefinition ActiveIndex
		{
			get { return activeIndex; }
			set
			{
				activeIndex = value;
				if (activeIndex != null)
					ActiveItem = new EditIndexViewModel(activeIndex, server, Events, NavigationService);
				NotifyOfPropertyChange(() => ActiveIndex);
			}
		}

		public object ActiveItem
		{
			get
			{
				return activeItem;
			}
			set
			{
				var deactivatable = activeItem as IDeactivate;
				if (deactivatable != null) deactivatable.Deactivate(close:true);

				var activatable = value as IActivate;
				if (activatable != null) activatable.Activate();

				activeItem = value; 
				NotifyOfPropertyChange(() => ActiveItem);
			}
		}

		void IHandle<IndexUpdated>.Handle(IndexUpdated message)
		{
			BeginRefreshIndexes();

			if (message.IsRemoved)
			{
				ActiveItem = null;
			}
		}

		void RefreshIndexes(int totalIndexCount)
		{
			Indexes.GetTotalResults = () => totalIndexCount;
			Indexes.LoadPage();
		}
	}
}