# Publish EmilyDesk with GitHub Desktop

This guide starts with the prepared `EmilyDesk-GitHub-Source` folder. Work in **Documents**, leaving your existing EmilyDesk working folder untouched.

1. Extract the supplied ZIP into **Documents**. It contains one `EmilyDesk-GitHub-Source` folder. Open that folder and confirm `README.md`, `Build.cmd`, `src`, and `OptionalWidgets` are directly inside it. Do not extract the ZIP onto the Desktop using an “Extract here” command.
2. Install [GitHub Desktop](https://desktop.github.com/) from its official site and sign in with your GitHub account.
3. In GitHub Desktop, choose **File → Add local repository…**, then select the `EmilyDesk-GitHub-Source` folder. If Desktop says it is not a repository, choose **Create a repository here**. Set its name to `EmilyDesk` and create it in that same folder. Do not select your old working folder.
4. In the **Changes** view, check that `src`, `OptionalWidgets`, `Assets`, `ThemePackages`, `Wallpapers`, the README, and `LICENSE` appear. `Output`, `Stage`, and `BuildLogs` should not. Enter `Initial EmilyDesk source release` as the summary and click **Commit to main**.
5. Click **Publish repository**. Choose `EmilyDesk` as the repository name. Leave **Keep this code private** checked if you want to review it on GitHub before opening it to everyone; uncheck it when you are ready for the public release. GitHub Desktop will upload the repository. This source is several hundred megabytes, so the first upload can take time.
6. Open the new repository on GitHub. Check that the README pictures load, `OptionalWidgets` is present, the **Sponsor** button links to PayPal, and the MIT license appears.

## Add the GitHub social preview

The file `.github/social-preview.jpg` is included in the folder. GitHub does not automatically use it as a social preview. On your repository page, open **Settings → General → Social preview → Edit** and upload that JPG. The README banner is already referenced automatically from `docs/EmilyDesk-README-Banner.png`.

## Publish a Windows installer

Run `Build.cmd` on Windows from this folder. After a successful build, use the repository's **Releases → Draft a new release** page. Make a version tag (for example, `v2.10.31`), attach `Output\EmilyDeskInstaller.exe`, and publish the release. Upload the optional `.emilywidget` packages from `Output\OptionalWidgets` as separate release assets if you want direct downloads. The source itself already includes all optional widgets.

Keep a backup of the installer and source ZIP outside the build's `Output` folder. A later build recreates `Output`.
