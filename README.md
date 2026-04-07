A windows form (.net standard compatible with .NET Framework 4.8) DLL to which can be passed a form.io definition like the one you find in the samples folder and a json containing the initial data
Basically the DLL should create a form based on the JSON definition and give the ability to the user to fill it managing all the validation and listbox choices.
Output of the DLL should be a JSON with only the raw data of the form.
A console test application is supplied to test differentt kind of forms
