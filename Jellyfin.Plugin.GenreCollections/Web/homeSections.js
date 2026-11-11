/*
 * Genre Collections - home screen rows.
 *
 * Adds rows to the Jellyfin web home screen (new movies, series with new episodes,
 * genre collections, one row per genre). It only reuses the web client's own markup
 * and CSS classes (verticalSection, emby-scroller, card, ...) and ships no CSS, so
 * the active theme (e.g. Abyss) styles the rows exactly like the native ones.
 */
(function () {
    'use strict';

    if (window.__genreCollectionsLoaded) {
        return;
    }
    window.__genreCollectionsLoaded = true;

    var CONTAINER_CLASS = 'genreCollectionsSections';
    var REFRESH_MS = 2 * 60 * 1000;
    var lastRender = 0;
    var rendering = false;

    function escapeHtml(value) {
        return String(value == null ? '' : value)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    function getApiClient() {
        return window.ApiClient || null;
    }

    function detailsUrl(item, serverId) {
        return '#/details?id=' + encodeURIComponent(item.Id) + '&serverId=' + encodeURIComponent(serverId);
    }

    function imageUrl(apiClient, item) {
        var tag = item.ImageTags && item.ImageTags.Primary;
        if (!tag) {
            return null;
        }
        return apiClient.getScaledImageUrl(item.Id, {
            type: 'Primary',
            fillWidth: 300,
            quality: 90,
            tag: tag
        });
    }

    function cardHtml(apiClient, item) {
        var serverId = apiClient.serverId();
        var url = detailsUrl(item, serverId);
        var img = imageUrl(apiClient, item);
        var name = escapeHtml(item.Name);
        var year = item.ProductionYear ? String(item.ProductionYear) : '';
        var played = item.UserData && item.UserData.Played;

        var html = '<div class="card overflowPortraitCard card-hoverable card-withuserdata"'
            + ' data-id="' + escapeHtml(item.Id) + '" data-serverid="' + escapeHtml(serverId) + '"'
            + ' data-type="' + escapeHtml(item.Type) + '" data-isfolder="' + (item.IsFolder ? 'true' : 'false') + '"'
            + ' data-action="link">';
        html += '<div class="cardBox cardBox-bottompadded">';
        html += '<div class="cardScalable">';
        html += '<div class="cardPadder cardPadder-overflowPortrait"></div>';
        html += '<a href="' + url + '" data-action="link" class="cardImageContainer coveredImage cardContent itemAction"'
            + ' aria-label="' + name + '"'
            + (img ? ' style="background-image:url(\'' + escapeHtml(img) + '\')"' : '') + '>';
        if (!img) {
            html += '<div class="cardText cardDefaultText">' + name + '</div>';
        }
        if (played) {
            html += '<div class="cardIndicators"><div class="playedIndicator indicator">'
                + '<span class="material-icons indicatorIcon check" aria-hidden="true"></span></div></div>';
        }
        html += '</a>';
        html += '</div>';
        html += '<div class="cardFooter">';
        html += '<div class="cardText cardTextCentered cardText-first"><bdi>' + name + '</bdi></div>';
        html += '<div class="cardText cardTextCentered cardText-secondary"><bdi>' + (year || '&nbsp;') + '</bdi></div>';
        html += '</div>';
        html += '</div></div>';
        return html;
    }

    function sectionHtml(apiClient, title, link, items) {
        var html = '<div class="verticalSection genreCollectionsSection">';
        html += '<div class="sectionTitleContainer sectionTitleContainer-cards padded-left">';
        if (link) {
            html += '<a is="emby-linkbutton" href="' + link + '" class="more button-flat button-flat-mini sectionTitleTextButton">';
            html += '<h2 class="sectionTitle sectionTitle-cards">' + escapeHtml(title) + '</h2>';
            html += '<span class="material-icons chevron_right" aria-hidden="true"></span>';
            html += '</a>';
        } else {
            html += '<h2 class="sectionTitle sectionTitle-cards">' + escapeHtml(title) + '</h2>';
        }
        html += '</div>';
        html += '<div is="emby-scroller" class="padded-top-focusscale padded-bottom-focusscale" data-centerfocus="true">';
        html += '<div is="emby-itemscontainer" class="itemsContainer scrollSlider focuscontainer-x">';
        for (var i = 0; i < items.length; i++) {
            html += cardHtml(apiClient, items[i]);
        }
        html += '</div></div></div>';
        return html;
    }

    function fetchItems(apiClient, query) {
        var base = {
            Recursive: true,
            Fields: 'PrimaryImageAspectRatio,ProductionYear',
            ImageTypeLimit: 1,
            EnableImageTypes: 'Primary',
            EnableTotalRecordCount: false
        };
        for (var key in query) {
            if (Object.prototype.hasOwnProperty.call(query, key)) {
                base[key] = query[key];
            }
        }
        return apiClient.getItems(apiClient.getCurrentUserId(), base)
            .then(function (result) { return (result && result.Items) || []; })
            .catch(function (err) {
                console.warn('[GenreCollections] item query failed', err);
                return [];
            });
    }

    function buildSections(apiClient, config) {
        var limit = config.ItemsPerSection || 20;
        var serverId = apiClient.serverId();
        var jobs = [];

        if (config.ShowLatestMovies) {
            jobs.push(fetchItems(apiClient, {
                IncludeItemTypes: 'Movie',
                SortBy: 'DateCreated,SortName',
                SortOrder: 'Descending',
                IsMissing: false,
                Limit: limit
            }).then(function (items) {
                return { title: config.Labels.LatestMovies, link: null, items: items };
            }));
        }

        if (config.ShowLatestSeries) {
            jobs.push(fetchItems(apiClient, {
                IncludeItemTypes: 'Series',
                SortBy: 'DateLastContentAdded,SortName',
                SortOrder: 'Descending',
                Limit: limit
            }).then(function (items) {
                return { title: config.Labels.LatestSeries, link: null, items: items };
            }));
        }

        if (config.ShowCollectionsRow && config.Collections.length) {
            var ids = config.Collections.map(function (c) { return c.Id; }).join(',');
            jobs.push(fetchItems(apiClient, {
                Ids: ids,
                Recursive: false,
                SortBy: 'SortName',
                SortOrder: 'Ascending'
            }).then(function (items) {
                return { title: config.Labels.Collections, link: null, items: items };
            }));
        }

        if (config.ShowGenreRows) {
            config.GenreRows.forEach(function (collection) {
                // Only the collection's own titles (movies and whole series):
                // a recursive query would also return the episodes and seasons inside each series.
                var query = {
                    ParentId: collection.Id,
                    Recursive: false,
                    IncludeItemTypes: 'Movie,Series',
                    SortBy: config.GenreRowSort === 'Random' ? 'Random' : (config.GenreRowSort + ',SortName'),
                    SortOrder: 'Descending',
                    Limit: limit
                };
                if (config.HideWatchedInGenreRows) {
                    query.IsPlayed = false;
                }
                jobs.push(fetchItems(apiClient, query).then(function (items) {
                    return {
                        title: collection.Name,
                        link: '#/details?id=' + encodeURIComponent(collection.Id) + '&serverId=' + encodeURIComponent(serverId),
                        items: items
                    };
                }));
            });
        }

        return Promise.all(jobs);
    }

    function getHomeTab() {
        var page = document.querySelector('#indexPage:not(.hide)');
        return page ? page.querySelector('#homeTab') : null;
    }

    function ensureContainer(homeTab, position) {
        var container = homeTab.querySelector('.' + CONTAINER_CLASS);
        var sections = homeTab.querySelector('.sections');
        if (!container) {
            container = document.createElement('div');
            container.className = CONTAINER_CLASS;
        }
        if (sections) {
            if (position === 'Top') {
                if (container.nextSibling !== sections) {
                    homeTab.insertBefore(container, sections);
                }
            } else if (sections.nextSibling !== container) {
                sections.parentNode.insertBefore(container, sections.nextSibling);
            }
        } else if (!container.parentNode) {
            homeTab.appendChild(container);
        }
        return container;
    }

    function render(force) {
        var apiClient = getApiClient();
        var homeTab = getHomeTab();
        if (!apiClient || !homeTab || rendering || !apiClient.getCurrentUserId()) {
            return;
        }
        var existing = homeTab.querySelector('.' + CONTAINER_CLASS);
        if (!force && existing && existing.childElementCount && Date.now() - lastRender < REFRESH_MS) {
            return;
        }

        rendering = true;
        apiClient.getJSON(apiClient.getUrl('GenreCollections/HomeSections'))
            .then(function (config) {
                if (!config || !config.Enabled) {
                    if (existing) {
                        existing.remove();
                    }
                    return null;
                }
                return buildSections(apiClient, config).then(function (sections) {
                    var html = '';
                    sections.forEach(function (s) {
                        if (s.items.length) {
                            html += sectionHtml(apiClient, s.title, s.link, s.items);
                        }
                    });
                    var container = ensureContainer(homeTab, config.Position);
                    container.innerHTML = html;
                    lastRender = Date.now();
                });
            })
            .catch(function (err) {
                console.warn('[GenreCollections] unable to render home sections', err);
            })
            .then(function () {
                rendering = false;
            });
    }

    // The home view fires "viewshow" every time it is displayed.
    document.addEventListener('viewshow', function (e) {
        var target = e.target;
        if (target && target.id === 'indexPage') {
            // Let the native sections render first.
            setTimeout(function () { render(false); }, 300);
        }
    }, true);

    // First load: the page may already be visible when this script runs.
    var tries = 0;
    var timer = setInterval(function () {
        tries++;
        if (getHomeTab() && getApiClient()) {
            render(false);
            clearInterval(timer);
        } else if (tries > 40) {
            clearInterval(timer);
        }
    }, 500);
})();
