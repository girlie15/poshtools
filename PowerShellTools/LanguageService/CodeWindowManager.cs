﻿/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System.Collections.Generic;
using System.Management.Automation.Language;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudioTools;

namespace PowerShellTools.LanguageService
{
    class CodeWindowManager : IVsCodeWindowManager
    {
        private readonly IVsCodeWindow _window;
        private readonly IWpfTextView _textView;
        //private readonly EditFilter _filter;
        private static readonly Dictionary<IWpfTextView, CodeWindowManager> _windows = new Dictionary<IWpfTextView, CodeWindowManager>();
        private DropDownBarClient _client;

        static CodeWindowManager()
        {
            PowerShellToolsPackage.Instance.OnIdle += OnIdle;
        }

        public CodeWindowManager(IVsCodeWindow codeWindow, IWpfTextView textView)
        {
            _window = codeWindow;
            _textView = textView;

            var model = PowerShellToolsPackage.ComponentModel;
            var adaptersFactory = model.GetService<IVsEditorAdaptersFactoryService>();
            IEditorOperationsFactoryService factory = model.GetService<IEditorOperationsFactoryService>();

        //    EditFilter editFilter = _filter = new EditFilter(textView, factory.GetEditorOperations(textView));
         //   IntellisenseController intellisenseController = IntellisenseControllerProvider.GetOrCreateController(model, textView);

        //    var adapter = adaptersFactory.GetViewAdapter(textView);
        //    editFilter.AttachKeyboardFilter(adapter);
         //   intellisenseController.AttachKeyboardFilter();

#if DEV11_OR_LATER
            var viewFilter = new TextViewFilter();
            viewFilter.AttachFilter(adapter);
#endif
        }

        private static void OnIdle(object sender, ComponentManagerEventArgs e)
        {
            foreach (var window in _windows)
            {
                if (e.ComponentManager.FContinueIdle() == 0)
                {
                    break;
                }

              //  window.Value._filter.DoIdle(e.ComponentManager);
            }
        }

        #region IVsCodeWindowManager Members

        public int AddAdornments()
        {
            _windows[_textView] = this;

            IVsTextView textView;

            if (ErrorHandler.Succeeded(_window.GetPrimaryView(out textView)))
            {
                OnNewView(textView);
            }

            if (ErrorHandler.Succeeded(_window.GetSecondaryView(out textView)))
            {
                OnNewView(textView);
            }

            //TODO: if (PowerShellToolsPackage.Instance.LangPrefs.NavigationBar)
            {
                return AddDropDownBar();
            }

            // return VSConstants.S_OK;
        }

        private int AddDropDownBar()
        {
            var text = _textView.TextBuffer.CurrentSnapshot.GetText();

            Token[] tokens;
            ParseError[] errors;

            var ast = Parser.ParseInput(text, out tokens, out errors);

            DropDownBarClient dropDown = _client = new DropDownBarClient(_textView, ast);

            IVsDropdownBarManager manager = (IVsDropdownBarManager)_window;

            IVsDropdownBar dropDownBar;
            int hr = manager.GetDropdownBar(out dropDownBar);
            if (ErrorHandler.Succeeded(hr) && dropDownBar != null)
            {
                hr = manager.RemoveDropdownBar();
                if (!ErrorHandler.Succeeded(hr))
                {
                    return hr;
                }
            }

            int res = manager.AddDropdownBar(2, dropDown);
            if (ErrorHandler.Succeeded(res))
            {
                _textView.TextBuffer.Properties[typeof(DropDownBarClient)] = dropDown;
            }
            return res;
        }

        private int RemoveDropDownBar()
        {
            if (_client != null)
            {
                IVsDropdownBarManager manager = (IVsDropdownBarManager)_window;
                _client.Unregister();
                _client = null;
                _textView.TextBuffer.Properties.RemoveProperty(typeof(DropDownBarClient));
                return manager.RemoveDropdownBar();
            }
            return VSConstants.S_OK;
        }

        public int OnNewView(IVsTextView pView)
        {
            // TODO: We pass _textView which may not be right for split buffers, we need
            // to test the case where we split a text file and save it as an existing file?
            return VSConstants.S_OK;
        }

        public int RemoveAdornments()
        {
            _windows.Remove(_textView);
            return RemoveDropDownBar();
        }

        public static void ToggleNavigationBar(bool fEnable)
        {
            foreach (var keyValue in _windows)
            {
                if (fEnable)
                {
                    ErrorHandler.ThrowOnFailure(keyValue.Value.AddDropDownBar());
                }
                else
                {
                    ErrorHandler.ThrowOnFailure(keyValue.Value.RemoveDropDownBar());
                }
            }
        }

        #endregion
    }

}
