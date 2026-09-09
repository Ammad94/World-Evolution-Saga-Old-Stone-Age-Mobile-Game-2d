@echo off

cd /d "D:\World Evolution Saga Old Stone Age"

echo Pulling latest changes from GitHub...

git reset --hard HEAD
git stash clear
git pull origin arena/01a07c59-world-evolution-saga-old-stone

echo.

echo Done! Open Unity and click Assets > Reimport All, then Play.

pause
