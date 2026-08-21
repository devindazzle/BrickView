# BrickView

A fast, visual browser for BrickLink Studio `.io` files.

BrickView lets you browse your BrickLink Studio model library visually using the thumbnails already stored inside your `.io` files.

![BrickView](docs/brickview-screenshot.png)

## Features

- Browse `.io` files with Studio thumbnails
- Fast asynchronous thumbnail loading
- Virtualized thumbnail grid with preloading
- Small, Medium and Large thumbnail views
- Live search with wildcard `*` support
- Sort by file name, created date or modified date
- Automatic folder monitoring and refresh
- Persistent view, sorting and folder settings
- Tags for organizing models
- Favorites for marking models you want to find quickly
- Studio-inspired dark interface
- Part counts from Studio metadata
- Context menu actions:
  - Open in Studio
  - Show in File Explorer
  - Copy file path
  - Copy file name

## What's New in 1.1

BrickView 1.1 adds several features that make it easier to organize and browse larger model libraries.

## Smart Search

BrickView includes a powerful Smart Search feature that lets you search models by name, tags, and favorite status.

Search is performed as you type, and multiple search criteria can be combined. All criteria must match for a model to be included in the results.

### Smart search

Enter any text to search model names and tags.

```text
castle
```

Finds models where either the model name or one of its tags contains `castle`.

Search is case-insensitive.

#### Search by name

Use `name:` to search only model names.

```text
name:castle
```

This will find models whose name contains `castle`, but will not match a tag containing `castle`.

#### Search by tag

Use `tag:` to search for a specific tag.

```text
tag:space
```

This finds models that have the `space` tag.

#### Favorites

Use `is:favorite` to show only favorite models.

```text
is:favorite
```

You can also search for models that are **not** marked as favorites:

```text
is:not-favorite
```

#### Excluding results

Prefix a search criterion with `-` to exclude matching models.

For example:

```text
-tag:space
```

This finds models that do not have the `space` tag.

You can also exclude text matches:

```text
-castle
```

This excludes models where `castle` appears in the model name or tags.

### Combining searches

Multiple search criteria can be combined. A model must satisfy **all** criteria to appear in the results.

For example:

```text
castle tag:medieval
```

Finds models where:

* `castle` appears in the model name or tags
* the model has the `medieval` tag

Another example:

```text
name:castle is:favorite
```

Finds favorite models whose name contains `castle`.

You can combine several criteria:

```text
name:castle tag:medieval is:favorite
```

### Quoted searches

Use quotation marks when searching for an exact phrase containing spaces.

```text
"black falcon"
```

This searches for the phrase `black falcon` in model names and tags.

Field-specific searches can also use quoted phrases:

```text
name:"black falcon"
```

or:

```text
tag:"black falcon"
```

#### Search syntax overview

| Syntax                | Description                         |
| --------------------- | ----------------------------------- |
| `castle`              | Search model names and tags         |
| `name:castle`         | Search model names only             |
| `tag:space`           | Search for a specific tag           |
| `is:favorite`         | Show favorite models                |
| `is:not-favorite`     | Show non-favorite models            |
| `-castle`             | Exclude models matching `castle`    |
| `-tag:space`          | Exclude models with the `space` tag |
| `"black falcon"`      | Search for a phrase                 |
| `name:"black falcon"` | Search for a phrase in model names  |
| `tag:"black falcon"`  | Search for a phrase in tags         |

#### Combining criteria

Search criteria can be combined freely:

```text
tag:castle is:favorite
```

```text
name:"black falcon" tag:medieval
```

```text
is:favorite -tag:modern
```

The more criteria you combine, the more specific your search becomes.

### Tags

Organize your models using custom tags.

- Add tags to models
- Remove tags from models
- Reuse existing tags
- Persistent tag storage
- Tags are independent from Favorites

### Favorites

Mark models as Favorites for quick access.

- Toggle Favorite status
- Persistent Favorites
- Easily identify models you want to keep track of

### Advanced Sorting

Sort your model library by:

- File name
- Created date
- Modified date

Each sort field supports ascending and descending order.

### Thumbnail Sizes

Choose between:

- Small
- Medium
- Large

Thumbnails are decoded at the appropriate resolution for the selected size.

### Automatic Folder Monitoring

BrickView automatically detects changes to the selected folder, including:

- New `.io` files
- Removed `.io` files
- Modified `.io` files
- Renamed `.io` files

### Performance Improvements

Version 1.1 improves the responsiveness of the application through:

- Asynchronous folder loading
- Debounced folder refresh
- Cancellation of outdated refresh operations
- Prioritized thumbnail loading
- Background thumbnail workers
- Prevention of duplicate active thumbnail loads
- Improved handling of large model collections

## Download

### Windows x64

Download the latest release:

**[BrickView 1.1](https://github.com/devindazzle/BrickView/releases/latest)**

Download the `BrickView-1.1.0-win-x64.zip` file, extract it, and run `BrickView.exe`.

BrickView is distributed as a self-contained Windows x64 application, so no separate .NET installation is required.

## Requirements

- Windows 10/11
- x64 processor

## Usage

1. Start BrickView.
2. Select the folder containing your BrickLink Studio `.io` files.
3. Browse your models visually.
4. Use search, tags, Favorites, sorting and the thumbnail size selector to find what you need.
5. Double-click a thumbnail to open the `.io` file using its Windows file association.

## License

See the repository for licensing information.
