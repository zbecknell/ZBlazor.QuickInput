﻿using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ZBlazor.QuickInput;

namespace ZBlazor
{
	public partial class QuickInput<TItem> : ComponentBase
		where TItem : class
	{
		#region FIELDS

		int selectedItemIndex = -1;
		bool isOpen = false;
		bool hasInputValue => InputValue != "";
		bool isMouseDown = false;
		bool isFocused = false;

		readonly FuzzyMatcher _fuzzyMatcher = new FuzzyMatcher();

		List<SearchItem<TItem>> SearchItems = new List<SearchItem<TItem>>();

		SearchItem<TItem>? lastSelectedItem = null;

		[Inject] private ILogger<QuickInput<TItem>>? Logger { get; set; }

		bool preventKeyDownDefault = false;

		string lastInputValue = "";

		/// <summary>
		/// True when the search items are loading.
		/// </summary>
		public bool IsLoading { get; private set; }

		int previousCycleDataCount = 0;

		bool hasLoaded;

		#endregion FIELDS

		#region PARAMETERS

		/// <summary>
		/// The actual value of the input.
		/// </summary>
		[Parameter] public string? InputValue { get; set; } = "";

		/// <summary>
		/// The collection of items to list as options in the <see cref="QuickInput{TItem}"/>.
		/// </summary>
		[Parameter] public IEnumerable<TItem> Data { get; set; } = null!;

		/// <summary>
		/// The type of filter to use. Defaults to <see cref="FilterType.Fuzzy"/>.
		/// </summary>
		[Parameter] public FilterType FilterType { get; set; }

		/// <summary>
		/// When the type of TItem is not a string, this denotes the field name that should be used to display the item text. Defaults to "Text".
		/// </summary>
		[Parameter] public string? TextField { get; set; } = "Text";

		/// <summary>
		/// Any additional class(es) to apply to the actual input box.
		/// </summary>
		[Parameter] public string Class { get; set; } = "";

		/// <summary>
		/// Any additional class(es) to apply to the outer div containing the input.
		/// </summary>
		[Parameter] public string ContainerClass { get; set; } = "";

		/// <summary>
		/// The width of the component. Defaults to "300px".
		/// </summary>
		[Parameter] public string Width { get; set; } = "300px";

		/// <summary>
		/// When true, the list will open when the component gains focus. Defaults to true.
		/// </summary>
		[Parameter] public bool OpenOnFocus { get; set; } = true;

		/// <summary>
		/// When true, the first match will be selected by default. Defaults to true;
		/// </summary>
		[Parameter] public bool SelectFirstMatch { get; set; } = true;

		/// <summary>
		/// Denotes the maximum number of items to show at a given time. Defaults to 5. Set to 0 for no limit.
		/// </summary>
		[Parameter] public int MaxItemsToShow { get; set; } = 5;

		/// <summary>
		/// Maximum height for the items container.
		/// </summary>
		[Parameter] public string? MaxItemsHeight { get; set; }

		/// <summary>
		/// When true, highlights matches in the list. Defaults to true.
		/// </summary>
		[Parameter] public bool HighlightMatches { get; set; } = true;

		/// <summary>
		/// When true, clears the input value when the Escape key is pressed and the input has focus. Defaults to true.
		/// </summary>
		[Parameter] public bool ClearOnEscape { get; set; } = true;

		/// <summary>
		/// When true, a clear button will show when the input has a value. Defaults to true.
		/// </summary>
		[Parameter] public bool ShowClearButton { get; set; } = true;

		/// <summary>
		/// When true, the input in cleared when a value is selected. Defaults to false.
		/// </summary>
		[Parameter] public bool ClearAfterSelection { get; set; } = false;

		/// <summary>
		/// When present, will be placed next to (after) the input. For things like icon overlays.
		/// </summary>
		[Parameter] public RenderFragment? InputAccessory { get; set; }

		/// <summary>
		/// When populated, will display when no matches are found for an input value.
		/// </summary>
		[Parameter] public RenderFragment? NoResultsView { get; set; }

		/// <summary>
		/// When populated, will display when no input is present.
		/// </summary>
		[Parameter] public RenderFragment? NoInputView { get; set; }

		/// <summary>
		/// When populated, will display when items are loading.
		/// </summary>
		[Parameter] public RenderFragment? LoadingView { get; set; }

		/// <summary>
		/// When populated, the matcher will also check other named fields on <code>TItem</code> for a filter match.
		/// </summary>
		[Parameter] public IEnumerable<string>? OtherMatchFields { get; set; }

		/// <summary>
		/// When true, shows the name and value of the "other match" fields when matched. Defaults to true. Note that if you use an ItemTemplate you need to pass a value for GetDisplayText if you don't want other matches to show.
		/// </summary>
		[Parameter] public bool ShowOtherMatches { get; set; } = true;

		/// <summary>
		/// Occurs when the user selects a value from the list.
		/// </summary>
		[Parameter] public EventCallback<TItem?> ValueChanged { get; set; }

		/// <summary>
		/// The actual value of the selected item.
		/// </summary>
		[Parameter] public TItem? Value { get; set; }

		/// <summary>
		/// When true, results will be ordered with shorter values first. Takes precedence above <see cref="PrioritizePrimaryMatch"/>. Defaults to false.
		/// </summary>
		[Parameter] public bool PrioritizeShorterValues { get; set; } = false;

		/// <summary>
		/// When other (non-primary) fields match, sort primary matches to the top always. Defaults to false.
		/// </summary>
		[Parameter] public bool PrioritizePrimaryMatch { get; set; } = false;

		/// <summary>
		/// When true, input not matching any search item is allowed, otherwise input is cleared when not matching any search items. Defaults to true.
		/// </summary>
		[Parameter] public bool AllowCustomValues { get; set; } = true;

		/// <summary>
		/// The type of the input element. Defaults to 'text'.
		/// </summary>
		[Parameter] public string InputType { get; set; } = "text";

		/// <summary>
		/// Occurs when the user changes the value inside the input.
		/// </summary>
		[Parameter] public EventCallback<string> OnInputValueChanged { get; set; }

		/// <summary>
		/// When present, the input will pass through this filter before performing the search.
		/// </summary>
		[Parameter] public Func<string?, string?>? InputValueFilter { get; set; }

		/// <summary>
		/// When true, a null value will be passed to <see cref="ValueChanged"/>. Defaults to true.
		/// </summary>
		[Parameter] public bool EmitNullOnInputClear { get; set; } = true;

		/// <summary>
		/// The repository to use for holding recent selected records. When populated, recents will be used, otherwise recent functionality will be disabled. Requires the <see cref="KeyField"/> to be populated with a valid unique key for <code>TItem</code>.
		/// </summary>
		[Parameter] public IRecentRepository? RecentRepository { get; set; }

		/// <summary>
		/// When present, is the field containing a unique key for the <code>TItem</code>.
		/// </summary>
		[Parameter] public string? KeyField { get; set; }

		/// <summary>
		/// The render fragment for displaying each item in the list.
		/// </summary>
		[Parameter] public RenderFragment<SearchItem<TItem>>? ItemTemplate { get; set; }

		/// <summary>
		/// The debounce delay in milliseconds between filtering results during typing. Defaults to 0.
		/// </summary>
		[Parameter] public int DebounceMilliseconds { get; set; } = 0;

		/// <summary>
		/// The initialization delay in milliseconds. Occurs before initializing the list of search items. Defaults to 100.
		/// </summary>
		[Parameter] public int InitializationDelayMilliseconds { get; set; } = 100;

		/// <summary>
		/// When true, the currently selected item in the list will be chosen when the Tab key is pressed. Defaults to false.
		/// </summary>
		[Parameter] public bool ChooseItemOnTab { get; set; }

		/// <summary>
		/// When true, the currently selected item in the list will be chosen on blur. Defaults to false.
		/// </summary>
		[Parameter] public bool ChooseItemOnBlur { get; set; }

		/// <summary>
		/// When true, search items will only populate the element is in focus. Defaults to false.
		/// </summary>
		[Parameter] public bool LazyLoad { get; set; }


		// TODO: fix
		/// <summary>
		/// When present, the base list of search items will be filtered by this predicate. Caution may not work yet!
		/// </summary>
		[Parameter] public Func<SearchItem<TItem>, bool> FilterPredicate { get; set; } = (i) => true;

		/// <summary>
		/// Any unmatched element attributes to be applied to the input.
		/// </summary>
		[Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object> InputAttributes { get; set; } = new Dictionary<string, object>();

		#endregion PARAMETERS

		#region LIFECYCLE

		/// <inheritdoc/>
		protected override async Task OnAfterRenderAsync(bool firstRender)
		{
			var currentCycleDataCount = Data?.Count() ?? 0;

			// TODO: this has a bug if the data were to be replaced with a different set with the same number of items
			if ((hasLoaded || !LazyLoad) && previousCycleDataCount != currentCycleDataCount)
			{
				previousCycleDataCount = currentCycleDataCount;
				await InitializeSearchItems();

				InputValue = await GetInputTextFromValue(Value);
			}

			await base.OnAfterRenderAsync(firstRender);
		}

		/// <inheritdoc/>
		protected override async Task OnParametersSetAsync()
		{
			selectedItemIndex = -1 + GetDefaultSelectedItemIndex();

			InputValue = await GetInputTextFromValue(Value);

			await base.OnParametersSetAsync();
		}

		#endregion LIFECYCLE

		#region EVENTS

		private Task OnValueChange(ChangeEventArgs args)
			=> FireValueChange(args);

		private async Task FireValueChange(ChangeEventArgs args, bool fromSelected = false)
		{
			InputValue = (args.Value as string) ?? "";

			if (OnInputValueChanged.HasDelegate)
			{
				await OnInputValueChanged.InvokeAsync(InputValue);
			}

			isOpen = OpenOnFocus || hasInputValue;

			await FilterDebounced();

			lastInputValue = InputValue;

			if (InputValue == "" && ValueChanged.HasDelegate)
			{
				await ValueChanged.InvokeAsync(null);
			}
		}

		private async Task OnSelected(SearchItem<TItem>? item)
		{
			lastSelectedItem = item;
			InputValue = item?.Text ?? "";

			Value = item?.DataObject;

			if (RecentRepository != null && item != null)
			{
				await RecentRepository.AddHit(item.Key);
				item.LastHit = DateTime.Now;
			}

			if (ValueChanged.HasDelegate)
			{
				await ValueChanged.InvokeAsync(item?.DataObject);
			}

			if (ClearAfterSelection)
			{
				await ClearInputValue();
			}

			await FireValueChange(new ChangeEventArgs { Value = InputValue }, true);

			isOpen = false;
		}

		private async Task OnFocus()
		{
			if (!hasLoaded)
			{
				IsLoading = true;
			}

			if (OpenOnFocus)
			{
				isOpen = true;
			}

			await Task.Delay(1);

			isFocused = true;

			if (LazyLoad)
			{
				var currentCycleDataCount = Data?.Count() ?? 0;

				// TODO: this has a bug if the data were to be replaced with a different set with the same number of items
				if (previousCycleDataCount != currentCycleDataCount)
				{
					previousCycleDataCount = currentCycleDataCount;
					await InitializeSearchItems();
				}
			}
		}

		private async Task OnBlur(FocusEventArgs args)
		{
			if (!isMouseDown)
			{

				if (ChooseItemOnBlur)
				{
					await ChooseSelected();
				}

				if (!AllowCustomValues &&
					(string.IsNullOrWhiteSpace(InputValue) || SearchItems.All(i => i.Text != InputValue)))
				{
					if (string.IsNullOrWhiteSpace(InputValue))
					{
						if (lastSelectedItem != null)
						{
							await OnSelected(null);
							await FilterDebounced();
						}
					}
					else
					{
						InputValue = lastSelectedItem?.Text;
						await OnSelected(lastSelectedItem);
					}
				}
				isOpen = false;
			}

			isFocused = false;
		}

		private async Task OnKeyDown(KeyboardEventArgs args)
		{
			preventKeyDownDefault = false;

			if (args.CtrlKey && args.ShiftKey && args.Key == "D")
			{
				Logger?.LogWarning("QuickInput Debug: {@Model}", new
				{
					InputValue,
					lastInputValue,
					Matches = SearchItems.Where(i => i.IsMatch),
					FilteredInputValue = (InputValueFilter != null ? InputValueFilter(InputValue) : null),
					hasInputValue,
					isOpen,
					IsLoading,
					IsFiltering,
					selectedItemIndex,
					Showing = SearchItems.Count(i => i.ShouldItemShow)
				});
			}

			switch (args.Code)
			{
				case "ArrowDown":
					if (selectedItemIndex + 1 >= actualShowingItems)
					{
						selectedItemIndex = 0;
					}
					else
					{
						selectedItemIndex += 1;
					}
					isOpen = true;
					break;
				case "ArrowUp":
					if (selectedItemIndex - 1 <= -1)
					{
						selectedItemIndex = actualShowingItems - 1;
					}
					else
					{
						selectedItemIndex -= 1;
					}
					isOpen = true;
					break;
				case "Tab":
					// If we're choosing item on blur, we don't need to also choose on tab
					if (ChooseItemOnTab && !ChooseItemOnBlur)
					{
						await ChooseSelected();
					}
					break;
				case "Enter":
					await ChooseSelected();
					break;
				case "Escape":
					// TODO: this doesn't really work
					preventKeyDownDefault = true;
					if (!hasInputValue)
					{
						isOpen = !isOpen;
					}
					else if (ClearOnEscape)
					{
						await ClearInputValue();
					}
					break;
			}
		}

		private async ValueTask ChooseSelected()
		{
			var selectedItem = SearchItems.SingleOrDefault(i => i.IsSelected);

			if (selectedItem != null)
			{
				await OnSelected(selectedItem);
			}

			await ClearSelectedValue();
			await FilterDebounced();
		}

		private void OnMouseDown(MouseEventArgs args)
		{
			isMouseDown = true;

			if (isFocused && OpenOnFocus && !hasInputValue && !isOpen)
			{
				isOpen = true;
			}
		}

		private void OnMouseUp(MouseEventArgs args)
		{
			isMouseDown = false;
		}

		#endregion EVENTS

		#region METHODS

		bool IsFiltering => currentSearchCts != null;

		CancellationTokenSource? currentSearchCts;

		private async ValueTask FilterDebounced(int? debounceMilliseconds = null, bool forceRefresh = false)
		{
			try
			{
				// Cancel any existing pending search, and begin a new one
				currentSearchCts?.Cancel();
				currentSearchCts = new CancellationTokenSource();
				var cancellationToken = currentSearchCts.Token;

				await Task.Delay(debounceMilliseconds ?? DebounceMilliseconds);
				if (!cancellationToken.IsCancellationRequested)
				{
					await Filter(cancellationToken, forceRefresh);
					currentSearchCts = null;
				}
			}
			catch (OperationCanceledException) { }
			catch (Exception ex)
			{
				Logger?.LogError(ex, ex.Message);
			}
		}

		private int GetDefaultSelectedItemIndex()
		{
			// Never select anything when we have no input and choose on blur/tab
			if (!hasInputValue && (ChooseItemOnBlur || ChooseItemOnTab))
			{
				return 0;
			}

			if (SelectFirstMatch)
			{
				return 1;
			}

			return 0;
		}

		private ValueTask Filter(CancellationToken? cancellationToken = null, bool forceRefresh = false)
		{
			if (!forceRefresh && lastInputValue == InputValue)
			{
				Logger?.LogDebug("No input change, filtering skipped");
				return default;
			}

			var stopwatch = new Stopwatch();
			stopwatch.Start();

			selectedItemIndex = -1 + GetDefaultSelectedItemIndex();

			string? workingInputValue = InputValue;

			if (InputValueFilter != null)
			{
				workingInputValue = InputValueFilter(workingInputValue);
			}

			foreach (var item in SearchItems)
			{
				bool wasMatching = item.IsMatch;

				ClearItem(item);

				if (cancellationToken?.IsCancellationRequested ?? false)
				{
					return default;
				}

				// If current input value starts with our last input value, we only need to evaluate
				// records that were matches last time
				if (!forceRefresh && lastInputValue.Length > 0 && (InputValue?.StartsWith(lastInputValue) ?? false) && !wasMatching)
				{
					continue;
				}

				MatchData match;

				if (FilterType == FilterType.Contains)
				{
					match = SimpleMatcher.MatchContains(workingInputValue ?? "", item.Text);
				}
				else if (FilterType == FilterType.StartsWith)
				{
					match = SimpleMatcher.MatchStartsWith(workingInputValue ?? "", item.Text);
				}
				else
				{
					match = _fuzzyMatcher.Match(workingInputValue ?? "", item.Text);
				}

				item.Matches = match.Matches;
				item.Score = match.Score;

				if (OtherMatchFields != null && OtherMatchFields.Count() > 0)
				{
					var itemType = item?.DataObject?.GetType();

					if (itemType == null)
					{
						break;
					}

					foreach (var otherField in OtherMatchFields)
					{
						if (cancellationToken?.IsCancellationRequested ?? false)
						{
							return default;
						}

						var otherFieldValue = itemType.GetProperty(otherField)?.GetValue(item!.DataObject)?.ToString();

						if (string.IsNullOrWhiteSpace(otherFieldValue))
						{
							continue;
						}

						// TODO: DRY
						MatchData otherMatch;

						if (FilterType == FilterType.Contains)
						{
							otherMatch = SimpleMatcher.MatchContains(workingInputValue ?? "", otherFieldValue);
						}
						else if (FilterType == FilterType.StartsWith)
						{
							otherMatch = SimpleMatcher.MatchStartsWith(workingInputValue ?? "", otherFieldValue);
						}
						else
						{
							otherMatch = _fuzzyMatcher.Match(workingInputValue ?? "", otherFieldValue);
						}

						if (otherMatch != null && otherMatch.Score > item!.Score)
						{
							item.Score = otherMatch.Score;
							item.Matches = otherMatch.Matches;
							item.OtherMatchFieldName = otherField;
							item.OtherMatchFieldValue = otherFieldValue;
						}
					}
				}
			}

			stopwatch.Stop();
			Logger?.LogDebug("Filtered input in {Elapsed} ms with {Matches} matches", stopwatch.Elapsed.Milliseconds, SearchItems.Count(i => i.IsMatch));

			return default;
		}

		int actualShowingItems;

		/// <summary>
		/// Gets the propertly ordered list of search items.
		/// </summary>
		protected virtual List<SearchItem<TItem>> GetOrderedSearchItems()
		{
			var result = new List<SearchItem<TItem>>();

			int actualItemsToShow = MaxItemsToShow == 0 ? SearchItems.Count : MaxItemsToShow;

			if (!hasInputValue)
			{
				if (RecentRepository != null)
				{
					result = SearchItems
						.OrderByDescending(i => i.LastHit)
						.Where(FilterPredicate)
						.Take(actualItemsToShow)
						.ToList();
				}
				else
				{
					result = SearchItems
						.Take(actualItemsToShow)
						.ToList();
				}

				actualShowingItems = result.Count;

				return result;
			}

			IOrderedEnumerable<SearchItem<TItem>> ordered = null!;

			if (PrioritizeShorterValues)
			{
				ordered = SearchItems
					.OrderBy(i => i.Text.Length);
			}

			if (PrioritizePrimaryMatch)
			{
				if (PrioritizeShorterValues)
				{
					ordered = ordered
						.ThenByDescending(i => i.IsPrimaryMatch);
				}
				else
				{
					ordered = SearchItems
						.OrderByDescending(i => i.IsPrimaryMatch);
				}
			}

			if (PrioritizePrimaryMatch || PrioritizeShorterValues)
			{
				result = ordered
					.ThenByDescending(i => i.Score)
					.Where(i => i.IsMatch)
					.Where(FilterPredicate)
					.Take(actualItemsToShow)
					.ToList();
			}
			else
			{
				result = SearchItems
				.OrderByDescending(i => i.Score)
				.Where(i => i.IsMatch)
				.Where(FilterPredicate)
				.Take(actualItemsToShow)
				.ToList();
			}

			actualShowingItems = result.Count;

			return result;
		}

		private void ClearItem(SearchItem<TItem> item)
		{
			item.Matches = null;
			item.Score = -100;
			item.OtherMatchFieldName = null;
			item.OtherMatchFieldValue = null;
		}

		private ValueTask<string?> GetInputTextFromValue(TItem? value)
		{
			if (value == null)
			{
				return new ValueTask<string?>("");
			}

			if (typeof(TItem) == typeof(string))
			{
				return new ValueTask<string?>((value as string)!);
			}
			else
			{
				if (TextField == null)
				{
					throw new ArgumentNullException(nameof(TextField));
				}

				var result = value?.GetType()?.GetProperty(TextField)?.GetValue(value, null)?.ToString() ?? "";

				return new ValueTask<string?>(result);
			}
		}

		private async ValueTask InitializeSearchItems()
		{
			var stopwatch = new Stopwatch();
			stopwatch.Start();

			if (!hasLoaded)
			{
				IsLoading = true;
			}

			await Task.Delay(InitializationDelayMilliseconds);

			if (Data == null)
			{
				Logger?.LogDebug("InitializeSearchItems: Data null, clearing any existing SearchItems.");
				SearchItems.Clear();
				return;
			}

			if (typeof(TItem) == typeof(string))
			{
				SearchItems = Data.Where(i => i != null).Select(i => new SearchItem<TItem>
				{
					Text = (i as string)!,
					DataObject = i,
					Key = GetKeyValueOrDefault(i)
				}).ToList();
			}
			else
			{
				if (TextField == null)
				{
					throw new ArgumentNullException(nameof(TextField));
				}

				SearchItems = Data.Where(i => i != null).Select(i => new SearchItem<TItem>
				{
					Text = i?.GetType()?.GetProperty(TextField)?.GetValue(i, null)?.ToString() ?? "",
					DataObject = i!,
					Key = GetKeyValueOrDefault(i!)
				}).ToList();
			}

			if (RecentRepository != null)
			{
				foreach (var item in SearchItems)
				{
					if (item.Key != null)
					{
						item.LastHit = await RecentRepository.GetHitForKey(item.Key);
					}

				}
			}

			stopwatch.Stop();

			Logger?.LogDebug("Initialized {Count} search items in {Elapsed} ms", SearchItems.Count, stopwatch.Elapsed.Milliseconds);

			await FilterDebounced(forceRefresh: true);

			hasLoaded = true;
			IsLoading = false;
		}

		private string GetKeyValueOrDefault(TItem item)
		{
			string? key = null;
			if (string.IsNullOrWhiteSpace(KeyField))
			{
				if (item is string stringItem && RecentRepository != null)
				{
					key = stringItem;
				}
			}
			else
			{
				key = typeof(TItem).GetProperty(KeyField)?.GetValue(item, null)?.ToString() ?? Guid.NewGuid().ToString();
			}

			return key ?? Guid.NewGuid().ToString();
		}

		private async ValueTask ClearInputValue()
		{
			lastSelectedItem = null;
			InputValue = "";
			Value = null;

			if (isFocused)
			{
				// Go ahead and close down if we don't open just for focus
				isOpen = OpenOnFocus;
			}

			if (ValueChanged.HasDelegate)
			{
				await ValueChanged.InvokeAsync(null!);
			}

			await ClearSelectedValue();
			await FilterDebounced(50);
		}

		private ValueTask ClearSelectedValue()
		{
			foreach (var item in SearchItems.Where(i => i.IsSelected))
			{
				item.IsSelected = false;
			}

			return default;
		}

		private bool ShouldItemShow(bool isMatch, int showingIndex)
		{
			// If we're not open, don't show anything
			if (!isOpen)
			{
				return false;
			}

			// If MaxItemsToShow has a limit and we are above that, never show
			if (MaxItemsToShow != 0 && showingIndex >= MaxItemsToShow)
			{
				return false;
			}

			// State is Open + In Limit + Matching, show it
			if (isMatch)
			{
				return true;
			}

			// State Open + In Limit + NOT Matching
			// If we DON'T have an input value, we show the default list
			if (!hasInputValue || IsFiltering)
			{
				return true;
			}

			return false;
		}

		#endregion METHODS
	}
}
