:: Deletes all the obj directories in the project.
pushd %~dp0\..
for /f %%d in ('dir /s /b obj') do rmdir /s /q %%d 
popd
