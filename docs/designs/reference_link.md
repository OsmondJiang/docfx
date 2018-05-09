# Reference Link
DocFX makes it easy to link documents together.

As we know, markdown provides a [syntax](https://daringfireball.net/projects/markdown/syntax#link) to create hyperlinks.
For example, the following syntax:
```markdown
[bing](http://www.bing.com)
```
Will render to:

```html
<a href="http://www.bing.com">bing</a>
```

Here the url in the link could be either absolute url pointing to external resource(`www.bing.com` in the above example),
or a relative url pointing to a local resource on the same server (for example, `about.html`).

## Link to a local resource

In DocFX, you can link to a locale resource:
  - Create a hyperlink, like `[docfx](docs/design/tableofcontent.md)`
  - Include a token, like `[!include[file name](subfolder/token.md)]`
  - Include a [nested toc](table-of-contents.md#link-to-another-toc-file), like `#[child](subfolder/toc.md)` or `#[child](subfolder/)`
  
Below are the details of each case and all of them use below folder structure example for detail explanation.

```
/
|- subfolder/
|  \- file2.md
|  \- toc.md
\- file1.md
\- toc.md
```

### Create a hyperlink using relative path

In DocFX, you can create a hyperlink using its relative path in the source directory.

For example, you can use relative path to reference `subfolder\file2.md` in `file1.md`:

```markdown
[file2](subfolder/file2.md)
```

or you can use relative path to reference `subfolder\file2.md` in `toc.md`:

```toc
#[file2 title](subfolder/file2.md)
```

DocFX converts it to a relative path in output folder structure:

```html
<a href="subfolder/file2.html">file2</a>
```

or 

```json
{
  "toc_title": "file2 title"
  "href": "subfolder/file2.html"
}
```

The resolved hyper link is the output path for file2.md, so you can see the source file name (`.md`) is replaced with output file name (`.html`).

> [!Note]
> DocFX does not simply replace the file extension here (`.md` to `.html`), it also tracks the mapping between input and
> output files to make sure source file path will resolve to correct output path.

> [!Note]
> The referenced files will be automatically built even it's not in the [content scope](config.md)

### Include a token using relaive path

The [file include](../spec/docfx_flavored_markdown.md#file-inclusion) syntax is using relative path to include a token file.

For example, if `file1.md` includes `subfolder\file2.md`:

```markdown
[!include[file2](subfolder/file2.md)]
```

All links in `file2.md` are relative to the `file2.md` itself, even when it's included by `file1.md`.

> [!Note]
> Please note that the file path in include syntax is handled differently than Markdown link.
> You can only use relative path to specify location of the included file.
> And DocFX doesn't require included file to be included in `docfx.yml`.
>
> [!Tip]
> Each file in `docfx.yml` will build into an output file. But included files usually don't need to build into individual
> topics. So it's not recommended to include them in `docfx.yml`, they should be excluded from the init scope `docfx.yml` if needed.

### Include a [nested toc](table-of-contents.md#link-to-another-toc-file) using relative path

The [toc](table-of-contents.md) syntax support to reference a nested toc using relative path or relative folder.

For example, if `toc.md` reference `subfolder\toc.md`:

```markdown
#[child](subfolder\toc.md)
```

or 

```markdown
#[child](subfolder\)
```

All links in `subfolder\toc.md` are relative to the `subfolder\toc.md` itself, even when it's included by `toc.md`.

### A easy way to write relative path

Sometimes you may find it's complicated to calculate relative path between two files.
DocFX also supports path starts with `~` to represent path relative to the root directory of your project (i.e. where `docfx.yml` is located).
This kind of path will also be validated and resolved during build.

For example, you can write the following links in `subfolder\file2.md` to reference `file1.md`:
 
```markdown
[file1](~/file1.md)

[file1](../file1.md)
```

Both will be resolved to `../file1.html`.

## Link to a dependency resource

Besides using file path to link to a local resource, DocFX also supports to link a resource stored in [dependenct repository](config.md)

For example you have a depenent repository defined in config:

```config
dependencies:
  dependent-repo-alias: https://github.com/dotnet/docfx-dependent#master
```

The folder structure in dependent repo is like below:

```
/
|- subfolder/
|  \- file2.md
|  \- toc.md
\- file1.md
\- toc.md
```

You can link a resouce stored in dependent repo:

```markdown
[dependeny file1](dependent-repo-alias\file1.md)
[dependeny file2](dependent-repo-alias\subfolder\file2.md)
```
[//]: # (what's the resolved href?)

## Link to an external resource
