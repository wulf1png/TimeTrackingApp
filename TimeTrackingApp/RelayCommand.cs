﻿using System;
using System.Windows.Input;

public class RelayCommand : ICommand
{
    private readonly Action<object> _execute;
    public RelayCommand(Action<object> execute) => _execute = execute;

    public event EventHandler CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object parameter) => true;
    public void Execute(object parameter) => _execute(parameter);

}